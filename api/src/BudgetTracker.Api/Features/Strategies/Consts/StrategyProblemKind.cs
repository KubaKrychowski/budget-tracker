namespace BudgetTracker.Api.Features.Strategies.Consts;

/// <summary>Rodzaj problemu znalezionego w grafie strategii — pokazywany na węźle jako czerwony znacznik.</summary>
/// <remarks>
/// Problem to NIE błąd zapisu: szkic z problemami zapisuje się zawsze. Węzły z problemami (poza
/// <see cref="EventWithoutChain"/> i <see cref="WaitDoesNotReturn"/>) symulator pomija, więc ich łańcuchy nic nie liczą.
/// Wartości idą do klienta jako tekst, a klient mapuje je na komunikaty — backend nie zwraca treści dla użytkownika.
/// </remarks>
public enum StrategyProblemKind
{
    /// <summary>Węzeł wymaga parametru, którego nie ma (kwota, miesiąc, warunek…).</summary>
    MissingParameter = 1,

    /// <summary>Żadna strzałka nie prowadzi do węzła, więc symulacja go nigdy nie wykona.</summary>
    NoIncomingEdge = 2,

    /// <summary>Zdarzenie bez skutku w gotówce nie ma żadnej akcji — nic po nim nie następuje.</summary>
    EventWithoutChain = 3,

    /// <summary>„Czekaj” nie ma następnika, więc nie wraca do warunku i łańcuch kończy się bez wyniku.</summary>
    WaitDoesNotReturn = 4,

    /// <summary>Węzeł leży na cyklu, który nie przechodzi przez „Czekaj” — pętla bez opóźnienia.</summary>
    Cycle = 5,

    /// <summary>Drugi kredyt albo druga poduszka docelowa — symulator obsługuje po jednym.</summary>
    Duplicate = 6,
}
