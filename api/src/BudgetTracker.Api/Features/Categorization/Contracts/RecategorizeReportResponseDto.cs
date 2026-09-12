namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>
/// Wynik przeliczenia kategorii dla wierszy, które są już w bazie.
/// </summary>
/// <remarks>
/// Liczby są tu po to, żeby po kliknięciu było widać, CO się stało — przeliczenie zmienia
/// dane, których użytkownik nie ogląda w tym momencie, więc „gotowe" bez liczb byłoby
/// prośbą o zaufanie.
/// </remarks>
/// <param name="Examined">Ile wierszy w ogóle wzięto pod uwagę (bez decyzji człowieka).</param>
/// <param name="Recategorized">Ile dostało INNĄ kategorię niż miało.</param>
/// <param name="MovedToReview">
/// Ile straciło kategorię i trafiło do przeglądu. Osobno od <paramref name="Recategorized"/>,
/// bo to jedyna zmiana, która dokłada użytkownikowi pracy — i jedyna, po której warto
/// zajrzeć do kolejki.
/// </param>
/// <param name="Unchanged">Ile zostało bez zmian.</param>
public sealed record RecategorizeReportResponseDto(
    int Examined,
    int Recategorized,
    int MovedToReview,
    int Unchanged);
