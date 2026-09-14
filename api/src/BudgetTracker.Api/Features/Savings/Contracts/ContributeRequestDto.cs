namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>„Wpłać na cel” — kwota umownie odkładana z oszczędności na rezerwację.</summary>
public sealed record ContributeRequestDto(decimal Amount);
