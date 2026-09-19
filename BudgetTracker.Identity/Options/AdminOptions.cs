namespace BudgetTracker.Identity.Options;

/// <summary>
/// Kto jest administratorem. Lista adresów w konfiguracji (sekrety / zmienne środowiskowe, nie repo): rola jest
/// nadawana kontom z tymi adresami przy starcie serwera i po potwierdzeniu adresu e-mail.
/// </summary>
/// <remarks>
/// ⚠️ Rola idzie WYŁĄCZNIE kontom z potwierdzonym adresem: bez tego ktoś mógłby zarejestrować cudzy adres z listy
/// i dostać uprawnienia, zanim właściciel adresu założy własne konto.
/// </remarks>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string[] Emails { get; init; } = [];

    public bool IsConfiguredAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(email) && Emails.Contains(email.Trim(), StringComparer.OrdinalIgnoreCase);
}
