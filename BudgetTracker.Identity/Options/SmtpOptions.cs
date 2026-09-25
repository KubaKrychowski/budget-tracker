namespace BudgetTracker.Identity.Options;

/// <summary>
/// Konfiguracja SMTP do wysyłki maili transakcyjnych (potwierdzenie konta, reset hasła, kod 2FA).
/// W Development wskazuje na MailHog (<c>appsettings.Development.json</c>) — na innych środowiskach
/// na SMTP z Azure; <see cref="Username"/>/<see cref="Password"/> celowo NIE mają defaultu w kodzie
/// ani w committed appsettings, bo trafiłyby do gita — ustaw je przez zmienne środowiskowe albo
/// konfigurację hostingu (np. Azure App Service Configuration).
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public required string Host { get; init; }

    public int Port { get; init; } = 587;

    /// <summary>MailHog (Development) nie ma TLS — w innych środowiskach zostaw <c>true</c>.</summary>
    public bool UseStartTls { get; init; } = true;

    /// <summary>Puste = brak uwierzytelnienia (tak działa MailHog).</summary>
    public string? Username { get; init; }

    public string? Password { get; init; }

    public required string FromAddress { get; init; }

    public string FromName { get; init; } = "Budżet tracker";

    /// <summary>
    /// Czy da się z tego wysłać choćby jednego maila.
    /// </summary>
    /// <remarks>
    /// ⚠️ Istnieje, bo wiązanie konfiguracji NIE wymusza <c>required</c> — obiekt powstaje przez refleksję
    /// i brakująca sekcja daje po prostu <c>Host = null</c>. Samo <c>ValidateOnStart()</c> tego nie łapie,
    /// dopóki nie ma reguły do sprawdzenia: serwer wstawał, a wywalała się dopiero pierwsza rejestracja,
    /// czyli błąd konfiguracji wychodził u użytkownika zamiast przy starcie.
    /// </remarks>
    public bool IsUsable => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
