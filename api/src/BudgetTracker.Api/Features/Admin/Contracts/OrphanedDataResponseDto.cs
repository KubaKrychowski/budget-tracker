namespace BudgetTracker.Api.Features.Admin.Contracts;

public sealed record OrphanedDataResponseDto(IReadOnlyList<OrphanedOwnerResponseDto> Owners);
