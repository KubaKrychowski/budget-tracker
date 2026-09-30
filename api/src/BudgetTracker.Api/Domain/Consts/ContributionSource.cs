using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Domain.Consts;

/// <summary>Z którego konta umownie odkładasz na cel.</summary>
/// <remarks>
/// ⚠️ Konwerter na TYPIE, bo aplikacja nie rejestruje globalnego <c>JsonStringEnumConverter</c> — ta sama pułapka co
/// przy <c>ReservationStatus</c>. Wartość jest też zapisana w kolumnie <c>jsonb</c> wpłat jako liczba, więc numeracja
/// od 1 jest częścią formatu danych.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ContributionSource>))]
public enum ContributionSource
{
    /// <summary>Konto oszczędnościowe — wpłata pomniejsza to, co da się jeszcze odłożyć z oszczędności.</summary>
    Savings = 1,

    /// <summary>
    /// Zwykłe konto — pieniądze zostają na koncie budżetu, ale są zarezerwowane na cel i wliczają się do limitu
    /// wskazanej kategorii.
    /// </summary>
    Regular = 2,
}
