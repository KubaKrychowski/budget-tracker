using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Czyste testy normalizacji — bez bazy i bez plików.
/// Wejścia mają kształt realnych opisów z wyciągu PKO, ale dane są wymyślone.
/// </summary>
public class DescriptionNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_empty_for_blank_input(string? input)
    {
        Assert.Equal(string.Empty, DescriptionNormalizer.Normalize(input));
    }

    [Fact]
    public void Lowercases_and_collapses_whitespace()
    {
        Assert.Equal("sklep pod dębem", DescriptionNormalizer.Normalize("  SKLEP   POD   DĘBEM  "));
    }

    [Fact]
    public void Strips_the_nested_address_label()
    {
        // W wyciągu PKO wartość pola „Lokalizacja” zaczyna się od kolejnej etykiety „Adres:”.
        Assert.Equal("market wiosenny miasto krakow",
            DescriptionNormalizer.Normalize("Adres: MARKET WIOSENNY Miasto: KRAKOW"));
    }

    [Fact]
    public void Strips_country_label()
    {
        Assert.Equal("market wiosenny", DescriptionNormalizer.Normalize("MARKET WIOSENNY Kraj: POL"));
    }

    [Theory]
    [InlineData("zakup 2026-08-31 sklep", "zakup sklep")]
    [InlineData("zakup 31.08.2026 sklep", "zakup sklep")]
    [InlineData("zakup 31/08/26 sklep", "zakup sklep")]
    public void Removes_dates(string input, string expected)
    {
        Assert.Equal(expected, DescriptionNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("platnosc karta 4111********1111 sklep")]
    [InlineData("platnosc karta 5555xxxx9999 sklep")]
    public void Removes_masked_card_numbers(string input)
    {
        var result = DescriptionNormalizer.Normalize(input);

        Assert.Equal("platnosc karta sklep", result);
    }

    [Fact]
    public void Removes_long_digit_runs_but_keeps_short_ones()
    {
        // Długie ciągi to numery referencyjne i rachunki — szum. Krótkie bywają częścią
        // nazwy sprzedawcy („market 24”), więc zostają. Usuwamy CYFRY, nie słowa:
        // „ref” zostaje, bo to zwykły wyraz — a w praktyce i tak nie trafia do opisu,
        // bo numer referencyjny czytamy z osobnego pola, nie z tytułu.
        Assert.Equal("market 24 ref", DescriptionNormalizer.Normalize("MARKET 24 REF 000123456789"));
    }

    [Fact]
    public void Keeps_dots_ampersands_and_hyphens_because_they_carry_meaning_in_names()
    {
        // „sklep.pl”, „a&b”, „bp-deteska” — usunięcie tych znaków skleiłoby wyrazy
        // i popsuło char n-gramy, na których stoi model (CLAUDE.md §3).
        Assert.Equal("sklep.pl a&b stacja-north", DescriptionNormalizer.Normalize("SKLEP.PL A&B STACJA-NORTH"));
    }

    [Fact]
    public void Produces_identical_output_for_the_same_merchant_written_differently()
    {
        // To jest cel całej normalizacji: ta sama transakcja u tego samego sprzedawcy
        // musi dać ten sam tekst, niezależnie od numerów i dat w opisie.
        var a = DescriptionNormalizer.Normalize("Tytuł: MARKET WIOSENNY 4821 Adres: MARKET WIOSENNY Miasto: KRAKOW");
        var b = DescriptionNormalizer.Normalize("Tytuł: MARKET WIOSENNY 9137 Adres: MARKET WIOSENNY Miasto: KRAKOW");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Does_not_depend_on_machine_locale()
    {
        // ToLowerInvariant zamiast ToLower(CurrentCulture) — inaczej ten sam wyciąg
        // dawałby różne opisy na różnych maszynach, a opis wchodzi do klucza deduplikacji.
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            var turkish = DescriptionNormalizer.Normalize("BIEDRONKA");

            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pl-PL");
            var polish = DescriptionNormalizer.Normalize("BIEDRONKA");

            Assert.Equal(polish, turkish);
            Assert.Equal("biedronka", polish);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
