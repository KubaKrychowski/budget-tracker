namespace BudgetTracker.Api.Features.Import.Contracts;

/// <summary>Wynik kroku „Podgląd". Niczego jeszcze nie zapisano.</summary>
public sealed record ImportPreviewResponseDto(
    int RowsInFile,
    int WillImport,
    int PendingReview,
    int SkippedDuplicates,
    IReadOnlyList<PreviewRowResponseDto> Rows);
