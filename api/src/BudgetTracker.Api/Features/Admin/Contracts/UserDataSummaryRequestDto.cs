namespace BudgetTracker.Api.Features.Admin.Contracts;

/// <summary>Właściciele (identyfikatory kont), dla których serwer tożsamości chce znać ilość danych.</summary>
public sealed record UserDataSummaryRequestDto(IReadOnlyList<Guid> UserIds);
