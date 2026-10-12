namespace BudgetTracker.Api.Features.Receipts.Services;

/// <summary>Sprawdza odczytaną sumę paragonu z sumą jego pozycji — czysta funkcja, bez Azure.</summary>
/// <remarks>
/// ⚠️ Pewność, z jaką model zwraca pole, nie wystarcza: na teście z prawdziwą usługą (2026-10-12) paragon o nietypowym układzie
/// dał sumę 20 zamiast 20,60 przy pewności 0,993, podczas gdy ceny pozycji dawały poprawne 20,60. Rozjazd z sumą pozycji jest
/// tanim, niezależnym sygnałem pomyłki, więc obniża pewność sumy — ekran pokaże „sprawdź” zamiast „pewny odczyt”.
/// Rozjazd bywa też uczciwy (rabat, zaokrąglenie, opłata niewidoczna jako pozycja) — wtedy użytkownik po prostu potwierdza sumę.
/// </remarks>
public static class ReceiptTotalCheck
{
    /// <summary>Dopuszczalna różnica na zaokrągleniach pojedynczych pozycji.</summary>
    public const decimal Tolerance = 0.01m;

    /// <summary>Pewność nadawana sumie, która nie zgadza się z pozycjami — poniżej progu „sprawdź”.</summary>
    public const float MismatchConfidence = 0.5f;

    /// <param name="itemPrices">Ceny WSZYSTKICH pozycji; pusta lista = nie ma z czym porównać, pewność zostaje.</param>
    public static float? Adjust(decimal? total, float? confidence, IReadOnlyList<decimal> itemPrices)
    {
        if (total is null || itemPrices.Count == 0) return confidence;

        return Math.Abs(itemPrices.Sum() - total.Value) <= Tolerance
            ? confidence
            : Math.Min(confidence ?? MismatchConfidence, MismatchConfidence);
    }
}
