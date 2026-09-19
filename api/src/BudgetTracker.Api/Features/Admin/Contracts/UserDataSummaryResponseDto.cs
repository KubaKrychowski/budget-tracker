namespace BudgetTracker.Api.Features.Admin.Contracts;

public sealed record UserDataSummaryResponseDto(Guid UserId, OwnerDataCountsResponseDto Counts);
