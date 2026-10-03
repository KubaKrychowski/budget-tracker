using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Kafelek na tablicy strategii — wartość zapisana w kolumnie <c>jsonb</c> razem ze strategią.</summary>
/// <remarks>
/// Zbiór parametrów jest wspólny dla wszystkich rodzajów (typowany nadzbiór, jak <see cref="StandingOrderRule"/>):
/// rodzaj <see cref="Type"/> decyduje, które pola są wymagane, a które ignorowane. Pole puste przy rodzaju,
/// który go wymaga, to „brakujący parametr” — problem węzła, nie błąd zapisu (szkic zapisuje się zawsze).
/// <para>
/// Pozycja <see cref="X"/>, <see cref="Y"/> to współrzędne na tablicy w pikselach — <c>double</c> jest tu w porządku,
/// bo to układ ekranu, nie pieniądze.
/// </para>
/// </remarks>
/// <param name="Id">Identyfikator węzła nadany przez klienta, stabilny między zapisami (adres połączeń).</param>
/// <param name="Type">Rodzaj kafelka.</param>
/// <param name="Title">Podpis kafelka; pusty = klient pokazuje nazwę rodzaju.</param>
/// <param name="X">Pozycja w poziomie.</param>
/// <param name="Y">Pozycja w pionie.</param>
/// <param name="Month">Miesiąc zdarzenia (klucz okresu — pierwszy dzień miesiąca); dla kredytu miesiąc startu.</param>
/// <param name="Amount">Kwota kafelka: wpływ, wydatek, nadwyżka, nadpłata, saldo kredytu, cel poduszki.</param>
/// <param name="Rate">Oprocentowanie roczne kredytu w procentach (13,29 = 13,29%).</param>
/// <param name="Installment">Rata miesięczna kredytu.</param>
/// <param name="Mode">Tryb rozliczenia nadpłaty.</param>
/// <param name="Metric">Co porównuje warunek.</param>
/// <param name="Comparison">Kierunek porównania warunku.</param>
/// <param name="Threshold">Próg warunku.</param>
public sealed record StrategyNode(
    string Id,
    StrategyNodeType Type,
    string Title,
    double X,
    double Y,
    DateOnly? Month,
    decimal? Amount,
    decimal? Rate,
    decimal? Installment,
    OverpaymentMode? Mode,
    StrategyConditionMetric? Metric,
    StrategyConditionComparison? Comparison,
    decimal? Threshold);
