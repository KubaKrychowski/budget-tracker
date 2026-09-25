namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Jedna wersja modelu w historii treningów.</summary>
/// <param name="Id">
/// Publiczny identyfikator wersji (<c>BusinessId</c>) — to nim przywraca się wersję przez
/// <c>POST /api/categorization/activate</c>.
/// ⚠️ NIE nazwa pliku: nazwa jest szczegółem magazynu i wyciekałaby układ kontenera na zewnątrz,
/// a przy dwóch treningach w tej samej sekundzie przestałaby być jednoznaczna.
/// </param>
/// <param name="Name">Nazwa pliku w magazynie — wyłącznie do podglądu i diagnostyki.</param>
/// <param name="Report">
/// Metryki z treningu, który tę wersję wyprodukował. <c>null</c> dla modelu, który powstał
/// przed wprowadzeniem historii — plik jest, ale nie wiadomo, na czym się uczył.
/// </param>
public sealed record ModelVersionResponseDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    bool IsActive,
    TrainingReportResponseDto? Report);
