using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Services;

/// <summary>
/// Kategoria, po której rozpoznajemy odkładanie i konto oszczędnościowe — <b>fallback do czasu #10</b>.
/// </summary>
/// <remarks>
/// ⚠️ Docelowo „odłożone" i stan konta czyta się z KONTA oszczędnościowego, bo tożsamość konta jest twarda,
/// a opis przelewu nie. Dziś konta nie biorą udziału w imporcie (<c>Transaction.AccountId</c> istnieje, ale
/// <c>CommitImportCommandHandler</c> go nie wypełnia), więc jedynym sygnałem jest kategoria nadawana regułą
/// na słowo „przeniesien" (<c>BaselineSeed</c>). Przelew opisany inaczej nie zostanie rozpoznany — i dlatego
/// odpowiedź celu niesie <c>HasAnySavings</c>, żeby ekran mógł o tym powiedzieć zamiast pokazywać zero jak fakt.
/// </remarks>
public sealed class SavingsCategory(AppDbContext db)
{
    public const string Name = "Oszczędności";

    /// <summary>Klucz kategorii; <c>null</c>, gdy takiej kategorii nie ma w bazie.</summary>
    public async Task<int?> IdAsync(CancellationToken ct) => await db.Categories
        .Where(c => c.Name == Name)
        .Select(c => (int?)c.Id)
        .FirstOrDefaultAsync(ct);
}
