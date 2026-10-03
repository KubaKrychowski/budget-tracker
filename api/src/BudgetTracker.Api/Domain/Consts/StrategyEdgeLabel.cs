using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Domain.Consts;

/// <summary>Etykieta połączenia: zwykłe albo jedno z dwóch wyjść warunku.</summary>
/// <remarks>Zapisana w <c>jsonb</c> grafu jako liczba — numeracja jest częścią formatu danych.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<StrategyEdgeLabel>))]
public enum StrategyEdgeLabel
{
    /// <summary>Zwykłe połączenie (wszystko poza wyjściami warunku).</summary>
    None = 1,

    /// <summary>Wyjście warunku, gdy jest spełniony.</summary>
    Yes = 2,

    /// <summary>Wyjście warunku, gdy nie jest spełniony.</summary>
    No = 3,
}
