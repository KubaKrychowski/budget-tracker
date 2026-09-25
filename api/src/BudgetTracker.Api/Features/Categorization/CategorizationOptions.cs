namespace BudgetTracker.Api.Features.Categorization;

/// <summary>Ustawienia kategoryzacji — sekcja <c>Categorization</c> w konfiguracji.</summary>
public sealed class CategorizationOptions
{
    public const string SectionName = "Categorization";
    /// <summary>
    /// Reguły specyficzne dla tej instalacji — wzorce, które działają wyłącznie dzięki historii
    /// konkretnej osoby (nazwa jej przychodni, jej cateringu). To dane osobowe, więc plik leży
    /// poza repo, jak model i zbiór treningowy. Brak pliku to stan normalny.
    /// </summary>
    public string LocalRulesPath { get; set; } = Path.Combine("data", "category-rules.json");

    /// <summary>
    /// Powyżej progu kategoria wchodzi automatycznie, poniżej transakcja idzie do przeglądu.
    /// 0.7 to ZAŁOŻENIE z handoffu (CLAUDE.md §9), nie pomiar — dlatego konfigurowalne,
    /// żeby strojenie nie wymagało rekompilacji.
    /// </summary>
    /// <remarks>
    /// ⚠️ Mieszkał w <c>ImportOptions</c>, dopóki pytał o model wyłącznie import. Odkąd pyta
    /// też przeliczanie kategorii, zostawienie go tam znaczyłoby, że slice kategoryzacji sięga
    /// po ustawienia slice'u importu — a Import już zależy od Categorization, więc powstałby
    /// cykl. Próg opisuje, kiedy ufamy MODELOWI, i to jest jego właściwe miejsce.
    /// </remarks>
    public decimal ConfidenceThreshold { get; set; } = 0.7m;

    /// <summary>
    /// Ile kategorii może łączyć słowo, żeby jeszcze otwierało bramkę słownika w <see cref="Services.MlCategorizer"/>.
    /// Słowo rozlane szerzej (etykieta z wyciągu, nazwa miasta) nie mówi nic o sprzedawcy i jest pomijane.
    /// </summary>
    /// <remarks>
    /// Wartość z pomiaru na zbiorze 1224 wierszy (zgłoszenie #9): przy 1 kategorii bramka zabierała automat
    /// 100 z 1161 pewnych i trafnych predykcji — za ostro. Przy 2–5 zabierała 1, a wszystkie losowe opisy
    /// z doklejonym „Miasto:" zatrzymywała. 3 zostawia zapas w obie strony: sprzedawca może wystąpić w kilku
    /// kategoriach (szum etykiet), a „miasto" (19) i nazwy miast (9+) wypadają wyraźnie.
    /// </remarks>
    public int VocabularyGateMaxCategories { get; set; } = 3;
}
