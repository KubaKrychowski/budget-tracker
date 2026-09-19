using BudgetTracker.Identity.Infrastructure;

namespace BudgetTracker.Identity.Tests;

public sealed class LocalizedIdentityErrorDescriberTests
{
    private readonly LocalizedIdentityErrorDescriber describer = new(TestLocalizer.Create());

    [Theory]
    [InlineData("pl", "Hasło musi mieć co najmniej 8 znaków.")]
    [InlineData("en", "The password must be at least 8 characters long.")]
    public void Password_too_short_mentions_the_required_length_in_the_request_language(string culture, string expected)
    {
        var error = TestLocalizer.InCulture(culture, () => describer.PasswordTooShort(PasswordPolicy.MinLength));

        Assert.Equal(nameof(describer.PasswordTooShort), error.Code);
        Assert.Equal(expected, error.Description);
    }

    [Theory]
    [InlineData("pl", "Konto z tym adresem e-mail już istnieje.")]
    [InlineData("en", "An account with this email address already exists.")]
    public void Duplicate_email_is_translated(string culture, string expected)
    {
        var error = TestLocalizer.InCulture(culture, () => describer.DuplicateEmail("a@b.c"));

        Assert.Equal(expected, error.Description);
    }

    [Fact]
    public void Password_policy_is_eight_characters_as_promised_in_the_hints()
    {
        // Podpowiedzi na ekranach i walidacja modeli opierają się na tej stałej; zmiana wymaga świadomej decyzji.
        Assert.Equal(8, PasswordPolicy.MinLength);
    }
}
