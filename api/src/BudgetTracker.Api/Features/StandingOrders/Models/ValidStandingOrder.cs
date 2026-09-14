using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.StandingOrders.Models;

/// <summary>Zlecenie po walidacji: teksty przycięte, miesiąc wyzerowany przy rytmie miesięcznym, reguły domenowe.</summary>
public sealed record ValidStandingOrder(
    Guid? BudgetId,
    string Name,
    decimal ExpectedAmount,
    StandingOrderRhythm Rhythm,
    int? DueMonth,
    IReadOnlyList<StandingOrderRule> Rules);
