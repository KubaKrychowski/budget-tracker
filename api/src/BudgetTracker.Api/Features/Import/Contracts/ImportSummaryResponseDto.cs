namespace BudgetTracker.Api.Features.Import.Contracts;

/// <summary>
/// Podsumowanie zapisanego importu — komplet liczb z ekranu „Podsumowanie" (krok 4 makiety).
/// </summary>
/// <param name="BudgetBalance">
/// Bilans początkowy budżetu plus WSZYSTKIE jego transakcje — ta sama formuła co na dashboardzie.
/// </param>
public sealed record ImportSummaryResponseDto(
    Guid BatchId,
    int RowsInFile,
    int Imported,
    int PendingReview,
    int SkippedDuplicates,
    decimal TotalExpenses,
    decimal TotalIncome,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    decimal? AverageConfidence,
    decimal BudgetBalance);
