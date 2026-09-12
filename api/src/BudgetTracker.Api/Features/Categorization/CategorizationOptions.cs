namespace BudgetTracker.Api.Features.Categorization;

/// <summary>Ustawienia kategoryzacji — sekcja <c>Categorization</c> w konfiguracji.</summary>
public sealed class CategorizationOptions
{
    public const string SectionName = "Categorization";

    /// <summary>Ścieżka do wytrenowanego modelu. Plik jest artefaktem, nie źródłem — nie trafia do repo.</summary>
    public string ModelPath { get; set; } = Path.Combine("data", "category-model.zip");

    /// <summary>Dane treningowe: opis, typ, kwota, kategoria. Zawierają nazwy sprzedawców, więc też poza repo.</summary>
    public string TrainingDataPath { get; set; } = Path.Combine("data", "training-set.csv");

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
}
