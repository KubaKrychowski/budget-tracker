namespace BudgetTracker.Api.Features.Import.Contracts;

/// <summary>Treść żądania zapisu importu.</summary>
/// <param name="BudgetId">Publiczny <c>BusinessId</c> budżetu wybranego w kroku 1.</param>
public sealed record CommitRequestDto(
    Guid BudgetId,
    string Bank,
    string FileName,
    IReadOnlyList<CommitRowRequestDto> Rows);
