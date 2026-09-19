using BudgetTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Zakłada konta seedów (<see cref="DevSeed"/>, <see cref="DemoSeed"/>) tylko tam, gdzie ich jeszcze nie ma.
/// </summary>
/// <remarks>
/// ⚠️ Konta seedu mają stały <c>BusinessId</c> i nie należą do żadnego użytkownika, więc PRZEŻYWAJĄ usunięcie jego danych
/// (ekran administratora kasuje wiersze właściciela, konta zostają). Strażnik seeda („są transakcje”) widzi wtedy pustą bazę
/// i seed ruszałby drugi raz — samo <c>AddRange</c> kończyło się błędem <c>IX_Accounts_BusinessId</c> i API nie wstawało.
/// Konto skasowane logicznie też blokuje unikalny indeks, więc wraca (<see cref="Entity.Restore"/>), zamiast się dublować.
/// </remarks>
internal static class SeedAccounts
{
    /// <summary>Zwraca konta w kolejności <paramref name="wanted"/>: istniejące albo dopiero co dodane.</summary>
    public static async Task<Account[]> EnsureAsync(AppDbContext db, IReadOnlyList<Account> wanted, CancellationToken ct)
    {
        var businessIds = wanted.Select(a => a.BusinessId).ToList();
        var existing = await db.Accounts.IgnoreQueryFilters()
            .Where(a => businessIds.Contains(a.BusinessId))
            .ToDictionaryAsync(a => a.BusinessId, ct);

        var result = new Account[wanted.Count];
        for (var i = 0; i < wanted.Count; i++)
        {
            if (existing.TryGetValue(wanted[i].BusinessId, out var account))
            {
                if (account.DeletedAt is not null) account.Restore();
                result[i] = account;
            }
            else
            {
                db.Accounts.Add(wanted[i]);
                result[i] = wanted[i];
            }
        }

        await db.SaveChangesAsync(ct);
        return result;
    }
}
