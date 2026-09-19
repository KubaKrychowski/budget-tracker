namespace BudgetTracker.Identity.Options;

/// <summary>
/// Certyfikaty serwera tożsamości poza Development: podpisujący (kto go ma, może wystawić dowolny token dla
/// dowolnego użytkownika) i szyfrujący (chroni kody autoryzacyjne i refresh tokeny). Pliki PFX leżą poza repo,
/// hasła dochodzą z sekretów albo zmiennych środowiskowych — nigdy z pliku, który idzie do gita.
/// </summary>
public sealed class ServerCertificatesOptions
{
    public const string SectionName = "Identity:Certificates";

    public CertificateFileOptions? Signing { get; init; }

    public CertificateFileOptions? Encryption { get; init; }
}

public sealed class CertificateFileOptions
{
    /// <summary>Ścieżka do pliku PFX na serwerze.</summary>
    public string? Path { get; init; }

    /// <summary>Hasło do PFX — wyłącznie przez sekrety / zmienne środowiskowe.</summary>
    public string? Password { get; init; }
}
