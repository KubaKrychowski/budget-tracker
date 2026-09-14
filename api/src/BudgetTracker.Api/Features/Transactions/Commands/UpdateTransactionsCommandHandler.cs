using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Features.Transactions.Consts;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Exceptions;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Commands;

/// <summary>
/// Zapis edycji inline — pojedynczej (jeden element) i masowej (wiele naraz) tym samym mechanizmem.
/// </summary>
public sealed class UpdateTransactionsCommandHandler(
    AppDbContext db, TransactionBudgetScope scope, TransactionListItemReader reader)
{
    /// <summary>Zapisuje edycje i zwraca zapisane wiersze, żeby front nie zgadywał stanu po zapisie.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Wszystko w JEDNEJ transakcji bazodanowej, wszystko albo nic. Częściowy zapis przy edycji
    /// dwudziestu wierszy zostawiłby użytkownika bez wiedzy, które przeszły.</item>
    /// <item>Walidacja PRZED otwarciem transakcji — nie ma po co jej zaczynać, jeśli wejście i tak jest
    /// do odrzucenia. Wszystkie wiersze naraz, nie pierwszy z brzegu: przy edycji masowej użytkownik ma
    /// dowiedzieć się o wszystkich pustych opisach.</item>
    /// <item>Zasięg budżetu jest częścią WARUNKU, nie sprawdzeniem po fakcie: wiersz spoza budżetu po prostu
    /// nie wejdzie do słownika, więc rzuci 404 — tak samo jak identyfikator, którego w ogóle nie ma.</item>
    /// <item>Reguła „człowiek poprawił kategorię" ma jedno miejsce — <see cref="ManualCategoryCorrection"/>
    /// (to samo wołane z importu). Wyczyszczenie kategorii do pustej wraca do kolejki przeglądu,
    /// bo transakcja znowu jej nie ma.</item>
    /// </list>
    /// </remarks>
    public async Task<IReadOnlyList<TransactionListItemResponseDto>> HandleAsync(
        UpdateTransactionsRequestDto request, CancellationToken ct)
    {
        if (request.Edits.Count == 0) return [];

        foreach (var edit in request.Edits) ValidateEdit(edit);

        var budgetIds = TransactionBudgetScope.Resolve(await scope.OptionsAsync(ct), request.BudgetIds, scope.Today());
        var ids = request.Edits.Select(e => e.Id).ToList();

        var transactions = await scope.TransactionsOf(budgetIds)
            .Where(t => ids.Contains(t.BusinessId))
            .ToDictionaryAsync(t => t.BusinessId, ct);

        var categoryKeys = await db.Categories
            .Select(c => new { c.Id, c.BusinessId })
            .ToDictionaryAsync(c => c.BusinessId, c => c.Id, ct);

        await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);

        foreach (var edit in request.Edits)
        {
            if (!transactions.TryGetValue(edit.Id, out var transaction))
                throw new TransactionNotFoundException(edit.Id);

            transaction.Edit(edit.Date, edit.Description.Trim(), edit.Amount);

            var newCategoryKey = ResolveCategory(edit.CategoryId, categoryKeys);
            if (newCategoryKey != transaction.CategoryId)
            {
                transaction.Recategorize(
                    newCategoryKey,
                    newCategoryKey is null ? TransactionStatus.PendingReview : ManualCategoryCorrection.Status,
                    ManualCategoryCorrection.Confidence);
            }
        }

        await db.SaveChangesAsync(ct);
        await dbTransaction.CommitAsync(ct);

        return await reader.ReadByIdsAsync(ids, ct);
    }

    /// <summary>
    /// Granice wiersza sprawdzane po stronie serwera, nie tylko przy polu w formularzu.
    /// </summary>
    /// <remarks>
    /// Pusty opis przechodziłby przez <c>IsRequired()</c> w EF (to NOT NULL, nie „niepusty"),
    /// zostawiając w tabeli wiersz bez nazwy — w aplikacji, której cała nawigacja opiera się
    /// na przeglądaniu i wyszukiwaniu PO OPISIE. Za długi z kolei rozbiłby się dopiero
    /// o <c>varchar(500)</c> w bazie, czyli 500 zamiast komunikatu.
    /// </remarks>
    private static void ValidateEdit(TransactionEditRequestDto edit)
    {
        var description = edit.Description?.Trim() ?? string.Empty;

        if (description.Length == 0)
            throw new TransactionDescriptionRequiredException();

        if (description.Length > TransactionLimits.DescriptionMaxLength)
            throw new TransactionDescriptionTooLongException();
    }

    /// <summary>Publiczny identyfikator kategorii → klucz zapisu; nieznany albo pusty = brak kategorii.</summary>
    private static int? ResolveCategory(Guid? businessId, IReadOnlyDictionary<Guid, int> keys) =>
        businessId is { } id && keys.TryGetValue(id, out var key) ? key : null;
}
