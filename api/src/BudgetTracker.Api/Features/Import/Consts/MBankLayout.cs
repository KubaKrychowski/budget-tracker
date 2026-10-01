namespace BudgetTracker.Api.Features.Import.Consts;

/// <summary>Układ pliku mBanku, wykryty po nagłówku tabeli operacji.</summary>
internal enum MBankLayout
{
    /// <summary>„Elektroniczne zestawienie operacji” — 8 kolumn, wiersz operacji spakowany w jedno pole.</summary>
    Statement = 1,

    /// <summary>„Lista operacji” — 5 kolumn, zwykły wiersz CSV z niecytowaną kwotą.</summary>
    OperationsList = 2,
}
