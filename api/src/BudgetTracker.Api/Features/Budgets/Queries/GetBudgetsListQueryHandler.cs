using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Features.Budgets.Services;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Features.Budgets.Queries;

/// <summary>Lista budżetów w ustawieniach — także usuniętych, bo tylko tu da się je przywrócić.</summary>
public sealed class GetBudgetsListQueryHandler(BudgetListItemReader reader, IOptions<BudgetOptions> options)
{
    /// <summary>Okno retencji, które modal usuwania obiecuje użytkownikowi.</summary>
    public int RetentionDays => options.Value.RetentionDays;

    public Task<IReadOnlyList<BudgetListItemResponseDto>> HandleAsync(CancellationToken ct) => reader.ReadAllAsync(ct);
}
