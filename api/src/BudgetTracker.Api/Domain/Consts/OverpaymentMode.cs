using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Domain.Consts;

/// <summary>Jak bank rozlicza nadpłatę kredytu.</summary>
/// <remarks>Zapisany w <c>jsonb</c> grafu jako liczba — numeracja jest częścią formatu danych.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<OverpaymentMode>))]
public enum OverpaymentMode
{
    /// <summary>Rata spada proporcjonalnie do spłaconej części kapitału, okres zostaje.</summary>
    ReduceInstallment = 1,

    /// <summary>Rata zostaje, kredyt kończy się szybciej.</summary>
    ShortenPeriod = 2,
}
