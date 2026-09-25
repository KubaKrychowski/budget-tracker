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

/// <summary>
/// Skąd wziąć jeden certyfikat serwera. Dwie drogi, wykluczające się: plik PFX na dysku albo para PEM-ów
/// podana wprost w konfiguracji. <see cref="Pem"/> wygrywa, gdy ustawione są obie.
/// </summary>
/// <remarks>
/// ⚠️ Droga „PEM w konfiguracji" istnieje dla hostingu, na którym nie ma sensownego miejsca na plik.
/// Na App Service katalog aplikacji jest nadpisywany przy każdym wdrożeniu, więc plik PFX trzeba by
/// dokładać do paczki — czyli trzymać materiał kryptograficzny obok kodu i pamiętać o nim przy każdym
/// wydaniu. Ustawienie aplikacji jest w tym układzie i bezpieczniejsze, i trwalsze.
/// </remarks>
public sealed class CertificateFileOptions
{
    /// <summary>Ścieżka do pliku PFX na serwerze.</summary>
    public string? Path { get; init; }

    /// <summary>Hasło do PFX — wyłącznie przez sekrety / zmienne środowiskowe. Dotyczy tylko <see cref="Path"/>.</summary>
    public string? Password { get; init; }

    /// <summary>
    /// Certyfikat w formacie PEM, zakodowany base64.
    /// </summary>
    /// <remarks>
    /// Base64, a nie goły PEM, bo wartość jedzie przez zmienną środowiskową: PEM jest wielolinijkowy,
    /// a łamanie linii przeżywa drogę przez konfigurację hosta w sposób zależny od hosta.
    /// </remarks>
    public string? Pem { get; init; }

    /// <summary>Klucz prywatny w formacie PEM, zakodowany base64. Wymagany razem z <see cref="Pem"/>.</summary>
    public string? PemKey { get; init; }
}
