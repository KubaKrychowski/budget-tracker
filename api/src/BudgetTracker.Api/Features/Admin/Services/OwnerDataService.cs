using System.Linq.Expressions;
using Azure.Storage.Blobs;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Admin.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Admin.Services;

/// <summary>
/// Operacje na danych CAŁEGO właściciela (konta): policzenie, trwałe usunięcie i przepisanie na inne konto.
/// Używane przez endpointy administracyjne wołane przez serwer tożsamości.
/// </summary>
/// <remarks>
/// <para>
/// Wszystko idzie przez <see cref="AsSystemAsync{T}"/>: to zadanie systemowe bez kontekstu żadnego użytkownika,
/// więc pod RLS w Postgresie widziałoby zero wierszy. <c>budget_jobs</c> (<c>BYPASSRLS</c>) obowiązuje tylko w
/// jednej transakcji — tak samo jak w <c>BudgetPurger</c>. Filtry EF (soft delete, właściciel) są wyłączane jawnie:
/// usunięcie konta ma zabrać także wiersze skasowane logicznie.
/// </para>
/// <para>
/// ⚠️ <b>Reguły kategoryzacji</b> (<c>CategoryRules</c>) należą do konta, ale NIE wchodzą do podsumowania ani do listy
/// „danych bez właściciela": reguły wspólne (bazowe) mają pusty <c>UserId</c>, więc właściciel „pusty" wyglądałby jak
/// sierota, a jego usunięcie skasowałoby reguły wszystkich. Usunięcie i przepisanie ruszają wyłącznie reguły konkretnego
/// konta i NIGDY wspólnych (<see cref="Domain.CategoryRule.SharedUserId"/>).
/// </para>
/// <para>
/// ⚠️ Kolejność kasowania to kolejność z <c>BudgetPurger</c>: dzieci przed rodzicem, transakcje przed importami
/// (klucze obce są <c>Restrict</c>). Zmiana kolejności kończy się błędem klucza obcego, nie cichą utratą danych.
/// </para>
/// </remarks>
public sealed class OwnerDataService(AppDbContext db, BlobServiceClient blobServiceClient)
{
    /// <summary>Wykonuje <paramref name="work"/> w jednej transakcji z rolą omijającą RLS.</summary>
    public async Task<T> AsSystemAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL ROLE budget_jobs", ct);

        var result = await work();

        await transaction.CommitAsync(ct);
        return result;
    }

    /// <summary>Ilość danych każdego właściciela, który ma jakiekolwiek wiersze.</summary>
    public async Task<IReadOnlyDictionary<Guid, OwnerDataCountsResponseDto>> CountByOwnerAsync(CancellationToken ct)
    {
        var budgets = await CountAsync(db.Budgets, x => x.UserId, ct);
        var budgetItems = await CountAsync(db.BudgetItems, x => x.UserId, ct);
        var transactions = await CountAsync(db.Transactions, x => x.UserId, ct);
        var imports = await CountAsync(db.ImportBatches, x => x.UserId, ct);
        var goals = await CountAsync(db.SavingsGoals, x => x.UserId, ct);
        var reservations = await CountAsync(db.SavingsReservations, x => x.UserId, ct);
        var standing = await CountAsync(db.StandingOrders, x => x.UserId, ct);
        var episodic = await CountAsync(db.EpisodicOrders, x => x.UserId, ct);
        var models = await CountAsync(db.ModelVersions, x => x.UserId, ct);

        var owners = budgets.Keys.Concat(budgetItems.Keys).Concat(transactions.Keys).Concat(imports.Keys)
            .Concat(goals.Keys).Concat(reservations.Keys).Concat(standing.Keys).Concat(episodic.Keys)
            .Concat(models.Keys)
            .Distinct();

        return owners.ToDictionary(o => o, o => new OwnerDataCountsResponseDto(
            budgets.GetValueOrDefault(o), budgetItems.GetValueOrDefault(o), transactions.GetValueOrDefault(o),
            imports.GetValueOrDefault(o), goals.GetValueOrDefault(o), reservations.GetValueOrDefault(o),
            standing.GetValueOrDefault(o), episodic.GetValueOrDefault(o), models.GetValueOrDefault(o)));
    }

    /// <summary>Najnowszy znacznik czasu z budżetów i transakcji każdego właściciela.</summary>
    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> LastChangeByOwnerAsync(CancellationToken ct)
    {
        var budgets = await db.Budgets.IgnoreQueryFilters()
            .GroupBy(b => b.UserId).Select(g => new { Owner = g.Key, Last = g.Max(b => b.CreatedAt) })
            .ToListAsync(ct);
        var transactions = await db.Transactions.IgnoreQueryFilters()
            .GroupBy(t => t.UserId).Select(g => new { Owner = g.Key, Last = g.Max(t => t.CreatedAt) })
            .ToListAsync(ct);

        return budgets.Concat(transactions)
            .GroupBy(x => x.Owner)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Last));
    }

    /// <summary>Kasuje magazyn plików właściciela razem z jego zawartością.</summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Usunięcie danych ma zabrać WSZYSTKO, także modele — inaczej po skasowanym koncie zostawałyby
    /// pliki, których nikt już nie potrafi przypisać do człowieka, a to najgorszy możliwy rodzaj resztki.
    /// </para>
    /// <para>
    /// Brak kontenera nie jest błędem: konto mogło go nigdy nie dostać (awaria API przy rejestracji),
    /// a usuwanie ma być idempotentne — serwer tożsamości ponawia je po przerwanym usuwaniu konta.
    /// </para>
    /// <para>
    /// ⚠️ Azure zwalnia nazwę kontenera z opóźnieniem. Założenie kontenera o tej samej nazwie zaraz po
    /// usunięciu kończy się konfliktem — dla identyfikatorów kont to bez znaczenia, bo nie wracają,
    /// ale nie próbuj tego użyć jako „czyszczenia" konta, które ma dalej działać.
    /// </para>
    /// </remarks>
    private async Task DeleteStorageAsync(Guid ownerId, CancellationToken ct) =>
        await UserBlobContainer.For(blobServiceClient, ownerId).DeleteIfExistsAsync(cancellationToken: ct);

    /// <summary>Kasuje trwale WSZYSTKIE wiersze właściciela (także skasowane logicznie) i zwraca, ile z czego zniknęło.</summary>
    public async Task<OwnerDataCountsResponseDto> DeleteAsync(Guid ownerId, CancellationToken ct)
    {
        var transactions = await db.Transactions.IgnoreQueryFilters().Where(x => x.UserId == ownerId).ExecuteDeleteAsync(ct);
        var imports = await db.ImportBatches.IgnoreQueryFilters().Where(x => x.UserId == ownerId).ExecuteDeleteAsync(ct);
        var budgetItems = await db.BudgetItems.IgnoreQueryFilters().Where(x => x.UserId == ownerId).ExecuteDeleteAsync(ct);
        var goals = await db.SavingsGoals.IgnoreQueryFilters().Where(x => x.UserId == ownerId).ExecuteDeleteAsync(ct);
        var reservations = await db.SavingsReservations.IgnoreQueryFilters().Where(x => x.UserId == ownerId).ExecuteDeleteAsync(ct);
        var standing = await db.StandingOrders.IgnoreQueryFilters().Where(x => x.UserId == ownerId).ExecuteDeleteAsync(ct);
        var episodic = await db.EpisodicOrders.IgnoreQueryFilters().Where(x => x.UserId == ownerId).ExecuteDeleteAsync(ct);
        var budgets = await db.Budgets.IgnoreQueryFilters().Where(x => x.UserId == ownerId).ExecuteDeleteAsync(ct);
        var models = await db.ModelVersions.IgnoreQueryFilters().Where(x => x.UserId == ownerId).ExecuteDeleteAsync(ct);
        await DeleteOwnRulesAsync(ownerId, ct);
        await DeleteStorageAsync(ownerId, ct);

        return new OwnerDataCountsResponseDto(
            budgets, budgetItems, transactions, imports, goals, reservations, standing, episodic, models);
    }

    /// <summary>Zmienia właściciela wszystkich wierszy z <paramref name="ownerId"/> na <paramref name="targetUserId"/>; nic nie kasuje.</summary>
    public async Task<OwnerDataCountsResponseDto> ReassignAsync(Guid ownerId, Guid targetUserId, CancellationToken ct)
    {
        var budgets = await db.Budgets.IgnoreQueryFilters().Where(x => x.UserId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, targetUserId), ct);
        var budgetItems = await db.BudgetItems.IgnoreQueryFilters().Where(x => x.UserId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, targetUserId), ct);
        var transactions = await db.Transactions.IgnoreQueryFilters().Where(x => x.UserId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, targetUserId), ct);
        var imports = await db.ImportBatches.IgnoreQueryFilters().Where(x => x.UserId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, targetUserId), ct);
        var goals = await db.SavingsGoals.IgnoreQueryFilters().Where(x => x.UserId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, targetUserId), ct);
        var reservations = await db.SavingsReservations.IgnoreQueryFilters().Where(x => x.UserId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, targetUserId), ct);
        var standing = await db.StandingOrders.IgnoreQueryFilters().Where(x => x.UserId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, targetUserId), ct);
        var episodic = await db.EpisodicOrders.IgnoreQueryFilters().Where(x => x.UserId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, targetUserId), ct);
        await ReassignOwnRulesAsync(ownerId, targetUserId, ct);

        return new OwnerDataCountsResponseDto(
            budgets, budgetItems, transactions, imports, goals, reservations, standing, episodic, ModelVersions: 0);
    }

    /// <summary>Kasuje trwale reguły konta (także skasowane logicznie); reguł wspólnych nie rusza nigdy.</summary>
    private async Task DeleteOwnRulesAsync(Guid ownerId, CancellationToken ct)
    {
        if (ownerId == CategoryRule.SharedUserId) return;

        await db.CategoryRules.IgnoreQueryFilters().Where(x => x.UserId == ownerId).ExecuteDeleteAsync(ct);
    }

    /// <summary>Przepisuje reguły konta na inne konto; reguł wspólnych nie rusza nigdy.</summary>
    private async Task ReassignOwnRulesAsync(Guid ownerId, Guid targetUserId, CancellationToken ct)
    {
        if (ownerId == CategoryRule.SharedUserId) return;

        await db.CategoryRules.IgnoreQueryFilters().Where(x => x.UserId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, targetUserId), ct);
    }

    private static async Task<Dictionary<Guid, int>> CountAsync<T>(
        DbSet<T> set, Expression<Func<T, Guid>> owner, CancellationToken ct) where T : class =>
        await set.IgnoreQueryFilters()
            .GroupBy(owner)
            .Select(g => new { Owner = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Owner, x => x.Count, ct);
}
