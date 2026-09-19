namespace BudgetTracker.Api.Features.Admin.Contracts;

/// <summary>Jeden wpis na każdego pytanego właściciela, także tych bez żadnych danych (same zera).</summary>
public sealed record UserDataSummariesResponseDto(IReadOnlyList<UserDataSummaryResponseDto> Users);
