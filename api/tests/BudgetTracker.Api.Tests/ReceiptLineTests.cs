using BudgetTracker.Api.Domain;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Czyszczenie nazwy produktu z paragonu — sama reguła, bez Azure. Nazwy są zmyślone.
/// </summary>
public sealed class ReceiptLineTests
{
    [Theory]
    [InlineData("PIEROGI RUSKI 320g-C", "PIEROGI RUSKI 320g")]
    [InlineData("  Chleb pszenny-A ", "Chleb pszenny")]
    [InlineData("Mleko 3,2%", "Mleko 3,2%")]
    public void The_tax_letter_glued_by_the_till_is_removed(string raw, string expected)
    {
        Assert.Equal(expected, ReceiptLine.CleanName(raw));
    }

    [Fact]
    public void A_letter_after_a_space_stays_because_it_can_belong_to_the_name()
    {
        // Łapie zbyt zachłanne obcinanie: „Witamina C” nie ma stracić litery.
        Assert.Equal("Witamina C", ReceiptLine.CleanName("Witamina C"));
    }

    [Fact]
    public void A_very_long_name_is_cut_to_the_limit()
    {
        Assert.Equal(ReceiptLine.MaxNameLength, ReceiptLine.CleanName(new string('x', 500)).Length);
    }
}
