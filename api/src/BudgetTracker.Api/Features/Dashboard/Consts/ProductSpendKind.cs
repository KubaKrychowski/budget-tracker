using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Features.Dashboard.Consts;

/// <summary>Rodzaj wiersza rozbicia kategorii na produkty z paragonów.</summary>
/// <remarks>
/// Idzie przez JSON jako NAZWA (nie liczba) i nie trafia do bazy. Nazwy etykiet „Bez kategorii” i „Bez paragonu” tłumaczy front po tym
/// polu — backend nie wysyła dla nich tekstu, żeby zmiana języka nie wymagała nowego zapytania.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ProductSpendKind>))]
public enum ProductSpendKind
{
    /// <summary>Produkty jednej kategorii produktu.</summary>
    Category = 1,

    /// <summary>Produkty z paragonów bez kategorii, a także część kwoty, której paragon nie rozbija (np. rabat, nieodczytana pozycja).</summary>
    Uncategorized = 2,

    /// <summary>Część kategorii bez paragonu z odczytanymi cenami pozycji.</summary>
    NoReceipt = 3,
}
