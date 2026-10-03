using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Domain.Consts;

/// <summary>Rodzaj kafelka na tablicy strategii.</summary>
/// <remarks>
/// ⚠️ Konwerter na TYPIE, bo aplikacja nie rejestruje globalnego <c>JsonStringEnumConverter</c> — ta sama pułapka co
/// przy <see cref="ContributionSource"/>. Wartość jest zapisana w kolumnie <c>jsonb</c> grafu jako LICZBA, więc
/// numeracja jest częścią formatu danych: nie zmieniaj numerów istniejących członków. Grupy mają osobne dziesiątki,
/// żeby nowy rodzaj dało się dopisać w swojej grupie bez przenumerowania.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<StrategyNodeType>))]
public enum StrategyNodeType
{
    /// <summary>Zdarzenie bez skutku w gotówce („Podwyżka”, „Koniec czesnego”) — tylko uruchamia łańcuch akcji.</summary>
    Trigger = 1,

    /// <summary>Wpływ jednorazowy (premia, wyrównanie): dopisuje kwotę do gotówki w swoim miesiącu.</summary>
    Income = 2,

    /// <summary>Wydatek jednorazowy (ubezpieczenie): odejmuje kwotę od gotówki w swoim miesiącu.</summary>
    Expense = 3,

    /// <summary>Stała nadwyżka miesięczna — dopisywana do gotówki co miesiąc od początku strategii.</summary>
    Surplus = 10,

    /// <summary>Kredyt: saldo, oprocentowanie roczne i rata. Źródło „długu” w symulacji.</summary>
    Loan = 11,

    /// <summary>Poduszka docelowa — kwota gotówki, po której osiągnięciu (przy zerowym długu) cel jest zrealizowany.</summary>
    CushionGoal = 12,

    /// <summary>Zmiana miesięcznej nadwyżki o kwotę (dodatnią albo ujemną) od miesiąca wykonania.</summary>
    IncreaseSurplus = 20,

    /// <summary>Nadpłata kredytu: kwota z gotówki, rata obniżona albo okres skrócony.</summary>
    Overpay = 21,

    /// <summary>Spłata całej reszty kredytu z gotówki (w granicach tego, co jest na koncie).</summary>
    PayOffLoan = 22,

    /// <summary>Cel oszczędzania — akcja do zastosowania w aplikacji, bez skutku w symulacji.</summary>
    SetSavingsGoal = 23,

    /// <summary>Rezerwacja na wydatek — akcja do zastosowania w aplikacji, bez skutku w symulacji.</summary>
    CreateReservation = 24,

    /// <summary>Zakończenie zlecenia stałego — akcja do zastosowania w aplikacji, bez skutku w symulacji.</summary>
    EndStandingOrder = 25,

    /// <summary>Limit kategorii — akcja do zastosowania w aplikacji, bez skutku w symulacji.</summary>
    SetLimit = 26,

    /// <summary>Warunek na gotówce i długu z dwoma wyjściami: tak i nie.</summary>
    Condition = 30,

    /// <summary>„Czekaj do kolejnego miesiąca” — jedyny sposób na pętlę: następniki wykonają się w następnym miesiącu.</summary>
    Wait = 31,

    /// <summary>Koniec łańcucha — znacznik „strategia zrealizowana”.</summary>
    End = 32,
}
