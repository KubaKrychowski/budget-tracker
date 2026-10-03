using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Domain.Consts;

/// <summary>Co porównuje warunek na tablicy strategii.</summary>
/// <remarks>Zapisane w <c>jsonb</c> grafu jako liczba — numeracja jest częścią formatu danych.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<StrategyConditionMetric>))]
public enum StrategyConditionMetric
{
    /// <summary>Gotówka (oszczędności) na koniec miesiąca.</summary>
    Cash = 1,

    /// <summary>Saldo długu na koniec miesiąca.</summary>
    Debt = 2,

    /// <summary>Gotówka minus dług — to, ile zostałoby po spłacie wszystkiego z gotówki.</summary>
    CashMinusDebt = 3,
}
