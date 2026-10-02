using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Domain.Consts;

/// <summary>
/// Strona przepływu, do której należy kategoria: wydatek albo wpływ. Do bazy trafia NAZWA wartości — kod słownika
/// <see cref="CategoryTypeDictionary"/>; zmiana nazwy wymaga migracji danych.
/// </summary>
/// <remarks>
/// ⚠️ Konwerter na NAZWY (jak przy <see cref="RuleDirection"/>): kontrakt kategorii jedzie przez JSON, a aplikacja nie
/// rejestruje konwertera globalnie. Numeracja jest szczegółem implementacji, nazwa jest kontraktem.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<CategoryType>))]
public enum CategoryType
{
    /// <summary>Kategoria wydatkowa — można na nią nakładać limity i przypisywać do niej dowolną kwotę (także zwrot).</summary>
    Expense = 1,

    /// <summary>Kategoria przychodowa — przyjmuje wyłącznie wpływy i nie ma limitów.</summary>
    Income = 2,
}
