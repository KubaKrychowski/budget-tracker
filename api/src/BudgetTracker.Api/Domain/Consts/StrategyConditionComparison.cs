using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Domain.Consts;

/// <summary>Kierunek porównania w warunku strategii.</summary>
/// <remarks>Zapisany w <c>jsonb</c> grafu jako liczba — numeracja jest częścią formatu danych.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<StrategyConditionComparison>))]
public enum StrategyConditionComparison
{
    /// <summary>Wartość jest co najmniej równa progowi.</summary>
    AtLeast = 1,

    /// <summary>Wartość jest co najwyżej równa progowi.</summary>
    AtMost = 2,
}
