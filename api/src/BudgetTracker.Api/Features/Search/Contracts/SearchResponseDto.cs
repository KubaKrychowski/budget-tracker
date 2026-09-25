namespace BudgetTracker.Api.Features.Search.Contracts;

/// <summary>Grupa wyników jednego rodzaju.</summary>
/// <param name="Kind">Klucz rodzaju (<see cref="Consts.SearchKind"/>) — nazwę grupy tłumaczy front.</param>
/// <param name="Total">
/// ILE jest wszystkich trafień tego rodzaju, nie ile zmieściło się w <paramref name="Hits"/>.
///
/// ⚠️ Bez tej liczby „3 transakcje" w menu wyglądałoby jak komplet, a nie jak podgląd pierwszych trzech
/// z dwudziestu jeden — i użytkownik przestałby szukać, choć jego pozycja istnieje.
/// </param>
/// <param name="Hits">Podgląd, przycięty do <see cref="Queries.GetSearchResultsQueryHandler.HitsPerGroup"/>.</param>
public sealed record SearchGroupResponseDto(
    string Kind,
    int Total,
    IReadOnlyList<SearchHitResponseDto> Hits);

/// <summary>
/// Wynik wyszukiwarki w nagłówku — wszystkie rodzaje jednym żądaniem.
/// </summary>
/// <param name="Query">Fraza, na którą ta odpowiedź odpowiada; front porzuca odpowiedzi do starszej frazy.</param>
/// <param name="Groups">Grupy w stałej kolejności (<see cref="Consts.SearchKind.Order"/>), puste pominięte.</param>
/// <param name="Total">Suma trafień ze wszystkich grup — do „Pokaż wszystkie wyniki”.</param>
/// <param name="BudgetId">Budżet, w którym szukaliśmy; <c>null</c>, gdy szukano po wszystkich.</param>
/// <param name="BudgetName">Jego nazwa — stopka menu mówi wprost, W CZYM szukaliśmy.</param>
/// <param name="AllBudgets">Czy zakres był poszerzony na wszystkie budżety.</param>
/// <remarks>
/// ⚠️ Zakres jest częścią ODPOWIEDZI, nie tylko żądania. Transakcje z konta oszczędnościowego leżą
/// w osobnym budżecie, więc pusty wynik bez podanego zakresu wygląda jak awaria aplikacji,
/// a nie jak świadome zawężenie.
/// </remarks>
public sealed record SearchResponseDto(
    string Query,
    IReadOnlyList<SearchGroupResponseDto> Groups,
    int Total,
    Guid? BudgetId,
    string? BudgetName,
    bool AllBudgets);
