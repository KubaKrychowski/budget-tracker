using BudgetTracker.Identity.Options;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// O tym, KTÓRĄ drogą idą maile, decyduje kompletność konfiguracji — a nie kod. Pomyłka w tym miejscu nie
/// przewraca serwera, tylko cicho psuje rejestrację: konto powstaje, link z potwierdzeniem nigdy nie
/// dolatuje, a użytkownik nie ma jak się zalogować i nie wie dlaczego.
/// </summary>
public sealed class EmailSenderSelectionTests
{
    private const string AnyConnectionString = "endpoint=https://example.communication.azure.com/;accesskey=test";
    private const string AnySender = "DoNotReply@example.azurecomm.net";

    [Theory]
    [InlineData(AnyConnectionString, AnySender, true)]
    // Połowa konfiguracji to nie konfiguracja: z samym connection stringiem nie ma od kogo wysłać,
    // a z samym adresem nie ma czym. W obu przypadkach ma wygrać SMTP, a nie wysypać się przy wysyłce.
    [InlineData(AnyConnectionString, null, false)]
    [InlineData(null, AnySender, false)]
    [InlineData(null, null, false)]
    [InlineData("", AnySender, false)]
    [InlineData("   ", AnySender, false)]
    [InlineData(AnyConnectionString, "   ", false)]
    public void Acs_is_used_only_when_both_the_connection_string_and_the_sender_are_present(
        string? connectionString, string? senderAddress, bool expected)
    {
        var options = new AcsEmailOptions { ConnectionString = connectionString, SenderAddress = senderAddress };

        Assert.Equal(expected, options.IsConfigured);
    }

    [Theory]
    [InlineData("localhost", "no-reply@example.com", true)]
    [InlineData(null, "no-reply@example.com", false)]
    [InlineData("localhost", null, false)]
    [InlineData("   ", "no-reply@example.com", false)]
    public void Smtp_without_a_host_or_a_sender_is_recognised_as_unusable(string? host, string? from, bool expected)
    {
        // ⚠️ Ta reguła istnieje, bo wiązanie konfiguracji NIE wymusza `required` — obiekt powstaje przez
        // refleksję, więc brak sekcji Smtp dawał Host = null i serwer wstawał jak gdyby nigdy nic.
        // Bez tego sprawdzenia błąd konfiguracji wychodził dopiero przy pierwszej rejestracji.
        var options = new SmtpOptions { Host = host!, FromAddress = from! };

        Assert.Equal(expected, options.IsUsable);
    }
}
