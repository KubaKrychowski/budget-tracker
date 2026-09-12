using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Infrastructure.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Queries;

/// <summary>Waluty do wyboru przy tworzeniu budżetu — kody ze słownika w bazie.</summary>
public sealed class GetAvailableCurrenciesQueryHandler(AppDbContext db)
{
    public async Task<IReadOnlyList<DictionaryResponseDto>> HandleAsync(CancellationToken ct) =>
        await db.Currencies
            .OrderBy(c => c.Code)
            .Select(c => new DictionaryResponseDto(c.Code))
            .ToListAsync(ct);
}
