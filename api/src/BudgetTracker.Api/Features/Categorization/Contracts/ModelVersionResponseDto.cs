namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Jedna wersja modelu leżąca na dysku.</summary>
/// <param name="Version">
/// Znacznik czasu z nazwy pliku — jednocześnie identyfikator wersji w
/// <c>POST /api/categorization/activate</c>. Format <c>yyyyMMddHHmmss</c>, więc sortowanie
/// leksykalne jest sortowaniem chronologicznym.
/// </param>
/// <param name="Report">
/// Metryki z treningu, który tę wersję wyprodukował. <c>null</c> dla modelu, który powstał
/// przed wprowadzeniem historii — plik jest, ale nie wiadomo, na czym się uczył.
/// </param>
public sealed record ModelVersionResponseDto(
    string Version,
    DateTimeOffset CreatedAt,
    bool IsActive,
    TrainingReportResponseDto? Report);
