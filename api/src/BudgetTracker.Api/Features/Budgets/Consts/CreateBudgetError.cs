namespace BudgetTracker.Api.Features.Budgets.Consts;

public enum CreateBudgetError
{
    None = 1,
    NameRequired = 2,
    UnsupportedCurrency = 3,
}