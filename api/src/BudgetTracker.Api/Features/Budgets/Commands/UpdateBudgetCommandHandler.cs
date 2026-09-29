using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>Zmiana nazwy, bilansu początkowego i dnia początku okresu rozliczeniowego budżetu.</summary>
public sealed class UpdateBudgetCommandHandler(AppDbContext db, BudgetLookup lookup, BudgetListItemReader reader)
{
    /// <remarks>Zmiana bilansu początkowego przelicza cały wykres bilansu — UI ostrzega o tym przed zapisem.</remarks>
    /// <exception cref="BudgetNameRequiredException">Nazwa pusta albo z samych spacji.</exception>
    /// <exception cref="BudgetPeriodStartDayInvalidException">Dzień początku okresu poza zakresem 1–28.</exception>
    public async Task<BudgetListItemResponseDto> HandleAsync(Guid businessId, UpdateBudgetRequestDto request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0) throw new BudgetNameRequiredException();

        if (request.PeriodStartDay is { } day && !BillingPeriod.IsValidStartDay(day)) throw new BudgetPeriodStartDayInvalidException();

        var budget = await lookup.FindIncludingDeletedAsync(businessId, ct);

        budget.Update(name, request.InitialBalance);
        if (request.PeriodStartDay is { } startDay) budget.ChangePeriodStartDay(startDay);

        await db.SaveChangesAsync(ct);
        return await reader.ReadOneAsync(businessId, ct);
    }
}
