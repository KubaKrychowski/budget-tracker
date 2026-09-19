using BudgetTracker.Identity.Services.Audit;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Dziennik audytu nie przechowuje adresu e-mail, tylko jego skrót — po nim da się sprawdzić „czy to konto było u nas",
/// ale nie da się odczytać adresu z samej tabeli. Skrót musi być stabilny, bo po nim się szuka.
/// </summary>
public sealed class AuditEmailHashTests
{
    [Fact]
    public void The_hash_is_the_lowercase_sha256_hex_of_the_address()
    {
        // SHA-256("anna@example.com") policzone niezależnie od kodu produkcyjnego.
        Assert.Equal(
            "f7d54c22dda5b1780276601cd005909b70543fc4a6ba72d735b4cb9bbb611f01",
            AuditEmailHash.Compute("anna@example.com"));
    }

    [Theory]
    [InlineData("  Anna@Example.com ")]
    [InlineData("ANNA@EXAMPLE.COM")]
    public void Case_and_surrounding_whitespace_do_not_change_the_hash(string variant) =>
        Assert.Equal(AuditEmailHash.Compute("anna@example.com"), AuditEmailHash.Compute(variant));

    [Fact]
    public void Different_addresses_give_different_hashes() =>
        Assert.NotEqual(AuditEmailHash.Compute("anna@example.com"), AuditEmailHash.Compute("ania@example.com"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_address_has_no_hash(string? email) => Assert.Null(AuditEmailHash.Compute(email));

    [Fact]
    public void The_hash_never_contains_the_address_and_fits_the_column()
    {
        var hash = AuditEmailHash.Compute("anna@example.com")!;

        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain("anna", hash);
    }
}
