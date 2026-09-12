using BudgetTracker.Api.Features.Import.Contracts;
using BudgetTracker.Api.Features.Import.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Import.Queries;

/// <summary>
/// Wszystko, czego stepper importu potrzebuje z góry: banki i budżety do kroku 1, kategorie do korekty w kroku 3.
/// </summary>
/// <remarks>
/// Jedno żądanie zamiast trzech — stepper i tak potrzebuje kompletu, zanim użytkownik cokolwiek kliknie.
/// Bez wyłączonych budżetów: import na taki budżet i tak skończyłby się 409, więc pokazywanie go w selektorze
/// byłoby zaproszeniem do ślepego zaułka.
/// </remarks>
public sealed class GetImportSourcesQueryHandler(IEnumerable<IStatementParser> parsers, AppDbContext db)
{
    public async Task<ImportSourcesResponseDto> HandleAsync(CancellationToken ct)
    {
        var budgets = await db.Budgets
            .Where(b => b.DisabledAt == null)
            .OrderByDescending(b => b.Month)
            .Select(b => new ImportBudgetOptionResponseDto(b.BusinessId, b.Name, b.Month))
            .ToListAsync(ct);

        var categories = await db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new ImportCategoryOptionResponseDto(c.BusinessId, c.Name))
            .ToListAsync(ct);

        var banks = parsers.Select(p => p.BankKey).Distinct().ToList();

        return new ImportSourcesResponseDto(banks, budgets, categories);
    }
}
