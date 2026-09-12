using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Import.Services;

/// <summary>Budżet, na który idzie import — jedno sprawdzenie dla podglądu i zapisu.</summary>
public sealed class ImportBudgetLookup(AppDbContext db)
{
    /// <summary>
    /// Publiczny identyfikator budżetu → encja, która przyjmuje nowe transakcje.
    /// </summary>
    /// <remarks>
    /// Nieznany kończy się <see cref="BudgetNotFoundException"/> (404), nie pustym wynikiem: import „bez budżetu"
    /// wyglądałby na sukces i zapisał transakcje donikąd.
    ///
    /// Wyłączony kończy się <see cref="BudgetDisabledException"/> (409). ⚠️ Sprawdzenie MUSI działać także
    /// w podglądzie, mimo że podgląd niczego nie zapisuje. Inaczej użytkownik przeszedłby cały stepper, poprawił
    /// kategorie i dopiero „Zapisz" powiedziałoby mu, że się nie da — całą pracę do kosza. Front i tak nie pokazuje
    /// wyłączonych w selektorze; to jest zabezpieczenie na wypadek, gdy budżet wyłączono już PO otwarciu steppera.
    /// </remarks>
    public async Task<Budget> FindAcceptingAsync(Guid businessId, CancellationToken ct)
    {
        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.BusinessId == businessId, ct)
            ?? throw new BudgetNotFoundException(businessId);

        if (budget.DisabledAt is not null) throw new BudgetDisabledException(budget.BusinessId);

        return budget;
    }
}
