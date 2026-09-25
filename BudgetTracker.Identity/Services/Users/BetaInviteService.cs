using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Identity.Services.Users;

/// <summary>Wynik próby dopisania adresu do listy zaproszeń.</summary>
public enum InviteOutcome
{
    Added,

    /// <summary>Adres już był na liście — nic nie dopisano.</summary>
    AlreadyInvited,

    /// <summary>Wpisu o tym identyfikatorze nie ma (ktoś usunął go w międzyczasie albo odświeżył POST).</summary>
    NotFound,

    Removed,
}

/// <summary>
/// Lista adresów, którym wolno założyć konto w zamkniętej becie: sprawdzenie przy rejestracji i obsługa
/// ekranu „Zaproszenia" w panelu administratora.
/// </summary>
/// <remarks>
/// Jedna klasa na odczyt przy rejestracji i na zarządzanie listą, bo to jedna reguła oglądana z dwóch stron.
/// Rozbicie na osobny „allow list" i osobny „CRUD" dałoby dwa miejsca, w których normalizuje się adres —
/// a rozjazd między nimi znaczyłby, że ekran pokazuje kogoś jako zaproszonego, a rejestracja go odrzuca.
/// </remarks>
public sealed class BetaInviteService(
    ApplicationDbContext db,
    TimeProvider clock,
    IOptions<RegistrationOptions> registration,
    IOptions<AdminOptions> admin)
{
    /// <summary>Czy rejestracja jest ograniczona do listy. <c>false</c> = każdy może założyć konto.</summary>
    public bool ClosedBeta => registration.Value.ClosedBeta;

    /// <summary>
    /// Czy ten adres może przejść rejestrację.
    /// </summary>
    /// <remarks>
    /// ⚠️ Adresy administratorów z konfiguracji przechodzą ZAWSZE, nawet przy pustej liście. Bez tego świeża
    /// instalacja z domyślnie włączoną betą byłaby zamknięta także dla osoby, która ma ją skonfigurować —
    /// zaproszenia dopisuje się z panelu, do którego trzeba wejść kontem, którego nie dałoby się założyć.
    /// To nie jest furtka: adres i tak musi być wpisany w konfiguracji serwera, a rolę administratora
    /// dostaje dopiero po potwierdzeniu adresu (patrz <see cref="AdminRoleService"/>).
    /// </remarks>
    public async Task<bool> IsAllowedToRegisterAsync(string? email, CancellationToken ct)
    {
        if (!ClosedBeta) return true;
        if (string.IsNullOrWhiteSpace(email)) return false;
        if (admin.Value.IsConfiguredAdmin(email)) return true;

        var normalized = BetaInvite.Normalize(email);
        return await db.BetaInvites.AnyAsync(i => i.Email == normalized, ct);
    }

    public Task<int> CountAsync(CancellationToken ct) => db.BetaInvites.CountAsync(ct);

    /// <summary>Strona listy, od najnowszych zaproszeń — świeżo dopisany adres ma być widoczny bez szukania.</summary>
    public async Task<IReadOnlyList<BetaInvite>> PageAsync(int page, int pageSize, CancellationToken ct) =>
        await db.BetaInvites
            .OrderByDescending(i => i.AddedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    /// <summary>Dopisuje adres. Powtórzenie jest bezpieczne: zwraca <see cref="InviteOutcome.AlreadyInvited"/>, nie błąd.</summary>
    public async Task<(InviteOutcome Outcome, BetaInvite? Invite)> AddAsync(string email, Guid actorId, CancellationToken ct)
    {
        var normalized = BetaInvite.Normalize(email);

        var existing = await db.BetaInvites.FirstOrDefaultAsync(i => i.Email == normalized, ct);
        if (existing is not null) return (InviteOutcome.AlreadyInvited, existing);

        var invite = new BetaInvite
        {
            Email = normalized,
            AddedAt = clock.GetUtcNow(),
            AddedByUserId = actorId,
        };

        db.BetaInvites.Add(invite);
        await db.SaveChangesAsync(ct);

        return (InviteOutcome.Added, invite);
    }

    public async Task<(InviteOutcome Outcome, BetaInvite? Invite)> RemoveAsync(Guid id, CancellationToken ct)
    {
        var invite = await db.BetaInvites.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invite is null) return (InviteOutcome.NotFound, null);

        db.BetaInvites.Remove(invite);
        await db.SaveChangesAsync(ct);

        return (InviteOutcome.Removed, invite);
    }
}
