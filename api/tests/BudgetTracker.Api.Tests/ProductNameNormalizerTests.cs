using BudgetTracker.Api.Features.ProductCategories.Services;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>Normalizacja nazw produktów z paragonów — sama reguła, bez bazy. Nazwy są zmyślone.</summary>
public sealed class ProductNameNormalizerTests
{
    [Theory]
    [InlineData("Pierogi ruskie 320 g", "pierogi ruskie")]
    [InlineData("MLEKO UHT 3,2% 1 l", "mleko uht")]
    [InlineData("Mleko UHT 2% 500ml", "mleko uht")]
    [InlineData("Woda 1,5l", "woda")]
    [InlineData("Jajka 10 szt.", "jajka")]
    [InlineData("Cola-Zero 0.5L", "cola zero")]
    public void Weights_volumes_and_percentages_are_dropped_so_variants_of_one_product_match(string raw, string expected)
    {
        Assert.Equal(expected, ProductNameNormalizer.Normalize(raw));
    }

    [Fact]
    public void Two_sizes_of_the_same_product_normalize_to_the_same_name()
    {
        // Łapie regułę nauczoną z jednego opakowania, która nie łapie drugiego.
        Assert.Equal(
            ProductNameNormalizer.Normalize("Mleko UHT 3,2% 1 l"),
            ProductNameNormalizer.Normalize("Mleko UHT 2% 500 ml"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("320 g")]
    public void A_missing_or_numeric_only_name_becomes_empty(string? raw)
    {
        Assert.Equal(string.Empty, ProductNameNormalizer.Normalize(raw));
    }

    [Fact]
    public void A_letter_after_a_number_with_no_unit_is_not_swallowed()
    {
        // „7up” nie jest „7 + unit”: litera po cyfrze to część nazwy, nie jednostka.
        Assert.Contains("up", ProductNameNormalizer.Normalize("7up"));
    }
}
