namespace BudgetTracker.Api.Features.Admin.Contracts;

/// <summary>Ile wierszy w każdej tabeli zostało usuniętych albo przepisanych na innego właściciela.</summary>
public sealed record OwnerDataChangeResponseDto(OwnerDataCountsResponseDto Counts);
