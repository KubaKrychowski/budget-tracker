namespace BudgetTracker.Identity.Options;

/// <summary>
/// Wysyłka maili przez Azure Communication Services — SDK, nie SMTP.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Klucz dostępu ACS (ten z zakładki „Keys" w portalu) działa TYLKO tędy. Przekaźnik SMTP
/// (<c>smtp.azurecomm.net</c>) go nie przyjmuje — tamta droga wymaga osobnego zasobu „SMTP Username"
/// powiązanego z rejestracją aplikacji w Entra ID, czyli trzech rzeczy do założenia ręcznie.
/// </para>
/// <para>
/// Gdy ta sekcja jest wypełniona, wysyłką zajmuje się <see cref="Services.AcsEmailSender"/>;
/// w przeciwnym razie <see cref="Services.SmtpEmailSender"/> (Development i MailHog).
/// </para>
/// </remarks>
public sealed class AcsEmailOptions
{
    public const string SectionName = "Acs:Email";

    /// <summary>Connection string zasobu ACS — wyłącznie z sekretów albo konfiguracji hostingu.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Adres nadawcy. Musi należeć do domeny podpiętej do tego zasobu ACS; przy domenie zarządzanej przez
    /// Azure ma postać <c>DoNotReply@&lt;guid&gt;.azurecomm.net</c>.
    /// </summary>
    public string? SenderAddress { get; init; }

    /// <summary>Czy da się z tego wysłać. Brak choćby jednej wartości znaczy „nie używamy ACS".</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) && !string.IsNullOrWhiteSpace(SenderAddress);
}
