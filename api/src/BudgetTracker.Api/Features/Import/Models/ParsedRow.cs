using System.Globalization;

namespace BudgetTracker.Api.Features.Import.Models;

/// <summary>
/// Wspólny kontrakt po parsowaniu — kształt niezależny od banku.
/// </summary>
/// <remarks>
/// To tutaj kończy się wiedza o formacie źródłowym: dalej (scalanie, kategoryzacja, zapis)
/// nic już nie wie, czy wiersz przyszedł z PKO, czy skądkolwiek indziej (CLAUDE.md §6).
/// </remarks>
/// <param name="Date">Data operacji — data księgowania, bez czasu i strefy.</param>
/// <param name="Amount">Ze znakiem: ujemne = wydatek, dodatnie = przychód.</param>
/// <param name="Description">Surowy opis złożony z pól banku, przed normalizacją.</param>
/// <param name="TransactionType">Typ z wyciągu, np. „Płatność kartą". Cecha dla kategoryzacji.</param>
/// <param name="ExternalReference">Numer referencyjny, jeśli bank go podaje. Null, gdy nie.</param>
public sealed record ParsedRow(
    DateOnly Date,
    decimal Amount,
    string Description,
    string TransactionType,
    string? ExternalReference)
{
    /// <summary>
    /// Klucz tożsamości transakcji — ten sam, którego używamy do wykrywania duplikatów
    /// wewnątrz pliku i względem bazy.
    /// </summary>
    /// <remarks>
    /// Numer referencyjny jest pierwszym członem, bo jest najmocniejszy. Ma go jednak tylko
    /// część wierszy (w próbce PKO 75,8%), więc dla reszty schodzimy na kwotę + datę + opis.
    /// Sama kwota + data byłaby za słaba — na realnych danych dawała 30 kolizji.
    ///
    /// ⚠️ Kwota jest formatowana jawnie, ze stałą skalą i kulturą niezmienną. Bez tego klucz się rozjeżdża po powrocie
    /// z bazy: <c>-10m</c> zapisane jako <c>numeric(18,2)</c> wraca jako <c>-10.00m</c>, a domyślne <c>ToString()</c>
    /// daje wtedy "-10.00" zamiast "-10". Deduplikacja NIGDY nie trafiłaby na istniejący rekord i każdy kolejny import
    /// dublowałby dane. Kultura niezmienna, bo separator dziesiętny nie może zależeć od ustawień maszyny.
    /// </remarks>
    public string IdentityKey()
    {
        var amount = Amount.ToString("0.00", CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(ExternalReference)
            ? $"{Date:yyyy-MM-dd}|{amount}|{Description}"
            : $"REF:{ExternalReference}|{Date:yyyy-MM-dd}|{amount}";
    }
}
