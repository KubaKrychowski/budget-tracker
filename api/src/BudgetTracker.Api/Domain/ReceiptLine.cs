namespace BudgetTracker.Api.Domain;

/// <summary>Jedna pozycja (produkt) z paragonu — to, co OCR odczytał w linii „Pierogi ruskie 320 g … 8,99”.</summary>
/// <remarks>
/// ⚠️ Pozycje są WARSTWĄ INFORMACYJNĄ paragonu, nie danymi budżetu: nie dzielą kwoty transakcji, nie mają kategorii i nic ich nie
/// sumuje do sald (DECISIONS.md §17, REWIZJA 2026-10-12). Zapisują się razem z paragonem w kolumnie <c>jsonb</c>, więc
/// kasują się z nim. Każde pole poza nazwą może być puste — OCR nie zawsze zna ilość albo cenę jednostkową.
/// </remarks>
/// <param name="Name">Nazwa z paragonu, przycięta; niepusta.</param>
/// <param name="Quantity">Ilość (szt. albo kg); <c>null</c>, gdy OCR jej nie odczytał.</param>
/// <param name="UnitPrice">Cena jednostkowa; <c>null</c>, gdy OCR jej nie odczytał.</param>
/// <param name="TotalPrice">Wartość pozycji; <c>null</c>, gdy OCR jej nie odczytał.</param>
public sealed record ReceiptLine(string Name, decimal? Quantity, decimal? UnitPrice, decimal? TotalPrice)
{
    /// <summary>Najdłuższa zapisywana nazwa — dłuższe ucinamy, żeby jedna dziwna linia nie rozdęła wiersza.</summary>
    public const int MaxNameLength = 200;

    /// <summary>Nazwa bez litery stawki PTU doklejonej przez kasę („PIEROGI RUSKI 320g-C” → „PIEROGI RUSKI 320g”), przycięta do limitu.</summary>
    /// <remarks>
    /// ⚠️ Obcinamy WYŁĄCZNIE przyrostek z myślnikiem i literą A–G na końcu: litera sama po spacji bywa częścią nazwy
    /// („Witamina C”), a stawki w kasach fiskalnych oznacza się literami A–G.
    /// </remarks>
    public static string CleanName(string raw)
    {
        var name = System.Text.RegularExpressions.Regex.Replace(raw.Trim(), "-[A-G]$", string.Empty).TrimEnd();
        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }
}
