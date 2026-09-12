using Microsoft.ML.Data;

namespace BudgetTracker.Api.Features.Categorization.Models;

/// <summary>
/// Wejście modelu. Kolumny odpowiadają nagłówkom w pliku treningowym,
/// żeby ten sam typ obsłużył i uczenie, i predykcję.
/// </summary>
public sealed class TransactionFeatures
{
    /// <summary>Opis PO normalizacji — model nigdy nie widzi surowego tekstu z wyciągu.</summary>
    [LoadColumn(0)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Typ operacji z wyciągu. Na realnych danych okazał się mocniejszy od opisu dla całych
    /// klas transakcji — wypłaty z bankomatu mają w opisie sam adres i tekstowo są nie do odróżnienia.
    /// </summary>
    [LoadColumn(1)]
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>
    /// Kwota ze znakiem. Niesie realny sygnał: zakup na stacji paliw poniżej 50 zł to sklep,
    /// powyżej — tankowanie, przy identycznej nazwie sprzedawcy.
    /// </summary>
    [LoadColumn(2)]
    public float Amount { get; set; }

    [LoadColumn(3)]
    public string Category { get; set; } = string.Empty;
}
