using System.Security.Cryptography.X509Certificates;
using BudgetTracker.Identity.Options;

namespace BudgetTracker.Identity.Infrastructure;

/// <summary>Ładuje certyfikat serwera z pliku PFX i od razu odrzuca taki, który się do niczego nie nadaje.</summary>
public static class ServerCertificateLoader
{
    /// <exception cref="InvalidOperationException">
    /// Brak ścieżki, brak pliku, zły format/hasło albo certyfikat bez klucza prywatnego lub już nieważny.
    /// Serwer ma się nie uruchomić, zamiast wystawiać tokeny podpisane byle czym.
    /// </exception>
    public static X509Certificate2 Load(CertificateFileOptions? options, string purpose)
    {
        if (string.IsNullOrWhiteSpace(options?.Path))
        {
            throw new InvalidOperationException(
                $"Brak ścieżki certyfikatu ({purpose}) w konfiguracji sekcji {ServerCertificatesOptions.SectionName}.");
        }

        if (!File.Exists(options.Path))
        {
            throw new InvalidOperationException($"Nie ma pliku certyfikatu ({purpose}): {options.Path}");
        }

        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12FromFile(options.Path, options.Password);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or IOException)
        {
            throw new InvalidOperationException(
                $"Nie udało się wczytać certyfikatu ({purpose}) z {options.Path} — zły format albo hasło.", ex);
        }

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException($"Certyfikat ({purpose}) nie zawiera klucza prywatnego: {options.Path}");
        }

        if (certificate.NotAfter.ToUniversalTime() < DateTime.UtcNow)
        {
            throw new InvalidOperationException($"Certyfikat ({purpose}) wygasł {certificate.NotAfter:u}: {options.Path}");
        }

        return certificate;
    }
}
