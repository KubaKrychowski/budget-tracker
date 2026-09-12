namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>
/// Skład zbioru treningowego. Rozbicie na źródła jest tu SENSEM ekranu, nie ozdobą:
/// odpowiada na pytanie „czy moje poprawki w ogóle się liczą", którego przed tą zmianą
/// nie dało się zadać — trening czytał wyłącznie plik.
/// </summary>
/// <param name="FromFile">Wiersze z pliku bazowego (<c>CategorizationOptions.TrainingDataPath</c>).</param>
/// <param name="FromCorrections">
/// Wiersze z bazy: wyłącznie decyzje człowieka o wydatkach — patrz <c>TrainingSetBuilder</c>.
/// </param>
/// <param name="Corrected">
/// Wiersze pliku bazowego, którym poprawka użytkownika ZMIENIŁA etykietę. To jest właściwy
/// dowód, że pętla uczenia działa: tych przykładów nie przybywa (są już w zbiorze), ale model
/// uczy się z nich czegoś INNEGO niż wcześniej.
///
/// ⚠️ Wcześniej lądowały w <paramref name="Duplicates"/> i były po prostu wyrzucane — więc korekta
/// kategorii wiersza obecnego w pliku nie zmieniała w modelu niczego. Akurat te poprawki są
/// najcenniejsze, bo powstają tam, gdzie stara etykieta była zła.
/// </param>
/// <param name="Duplicates">
/// Poprawki odsiane, bo ten sam przykład jest już w pliku Z TĄ SAMĄ kategorią. Plik powstał
/// z TYCH SAMYCH realnych transakcji, więc bez odsiewania te same wiersze weszłyby dwa razy
/// i przeważyły zbiór w stronę tego, co już w nim jest. Liczba jest pokazywana, bo inaczej
/// „poprawek: 300, razem: 1250" wyglądałoby na błąd arytmetyczny.
/// </param>
public sealed record TrainingSetCompositionResponseDto(
    int FromFile,
    int FromCorrections,
    int Corrected,
    int Duplicates)
{
    public int Total => FromFile + FromCorrections;
}
