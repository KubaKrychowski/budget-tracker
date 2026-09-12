using BudgetTracker.Api.Domain.Consts;

using BudgetTracker.Api.Features.Budgets.Consts;

namespace BudgetTracker.Api.Features.Budgets.Contracts;

public record CreateBudgetResponseDto(BudgetResponseDto? Budget, CreateBudgetError Error);