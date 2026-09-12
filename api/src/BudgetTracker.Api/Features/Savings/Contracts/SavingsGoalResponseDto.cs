namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Aktywny cel oszczędnościowy.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> celu.</param>
/// <param name="StartedOn">Pierwszy miesiąc obowiązywania.</param>
public sealed record SavingsGoalResponseDto(Guid Id, decimal Amount, DateOnly StartedOn);
