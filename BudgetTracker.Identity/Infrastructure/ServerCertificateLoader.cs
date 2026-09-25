using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using BudgetTracker.Identity.Options;

namespace BudgetTracker.Identity.Infrastructure;

/// <summary>Ładuje certyfikat serwera i od razu odrzuca taki, który się do niczego nie nadaje.</summary>
public static class ServerCertificateLoader
{
    /// <summary>
    /// Certyfikat z pliku PFX albo z pary PEM-ów w konfiguracji — patrz <see cref="CertificateFileOptions"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Brak konfiguracji, brak pliku, zły format/hasło albo certyfikat bez klucza prywatnego lub już nieważny.
    /// Serwer ma się nie uruchomić, zamiast wystawiać tokeny podpisane byle czym.
    /// </exception>
    public static X509Certificate2 Load(CertificateFileOptions? options, string purpose)
    {
        if (options is null)
        {
            throw new InvalidOperationException(
                $"Brak konfiguracji certyfikatu ({purpose}) w sekcji {ServerCertificatesOptions.SectionName}.");
        }

        var certificate = !string.IsNullOrWhiteSpace(options.Pem)
            ? LoadFromPem(options, purpose)
            : LoadFromFile(options, purpose);

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException($"Certyfikat ({purpose}) nie zawiera klucza prywatnego.");
        }

        if (certificate.NotAfter.ToUniversalTime() < DateTime.UtcNow)
        {
            throw new InvalidOperationException($"Certyfikat ({purpose}) wygasł {certificate.NotAfter:u}.");
        }

        return certificate;
    }

    private static X509Certificate2 LoadFromFile(CertificateFileOptions options, string purpose)
    {
        if (string.IsNullOrWhiteSpace(options.Path))
        {
            throw new InvalidOperationException(
                $"Brak ścieżki certyfikatu ({purpose}) w konfiguracji sekcji {ServerCertificatesOptions.SectionName}.");
        }

        if (!File.Exists(options.Path))
        {
            throw new InvalidOperationException($"Nie ma pliku certyfikatu ({purpose}): {options.Path}");
        }

        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(options.Path, options.Password);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            throw new InvalidOperationException(
                $"Nie udało się wczytać certyfikatu ({purpose}) z {options.Path} — zły format albo hasło.", ex);
        }
    }

    /// <remarks>
    /// ⚠️ Para przechodzi przez eksport do PKCS#12 i ponowne wczytanie, zamiast zostać tym, co zwraca
    /// <see cref="X509Certificate2.CreateFromPem(ReadOnlySpan{char}, ReadOnlySpan{char})"/>. Tamta metoda daje
    /// certyfikat z kluczem EFEMERYCZNYM, którego część dostawców kryptografii nie przyjmuje do podpisywania —
    /// objawia się to wyjątkiem dopiero przy pierwszej próbie wydania tokenu, a nie przy starcie. Przejście
    /// przez PKCS#12 daje klucz, który zachowuje się tak samo jak wczytany z pliku PFX.
    /// </remarks>
    private static X509Certificate2 LoadFromPem(CertificateFileOptions options, string purpose)
    {
        if (string.IsNullOrWhiteSpace(options.PemKey))
        {
            throw new InvalidOperationException(
                $"Podano certyfikat ({purpose}) w PEM, ale bez klucza prywatnego — uzupełnij PemKey w sekcji {ServerCertificatesOptions.SectionName}.");
        }

        var certificatePem = DecodeBase64(options.Pem!, purpose, "certyfikatu");
        var keyPem = DecodeBase64(options.PemKey, purpose, "klucza prywatnego");

        try
        {
            using var ephemeral = X509Certificate2.CreateFromPem(certificatePem, keyPem);
            return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"Nie udało się złożyć certyfikatu ({purpose}) z pary PEM — zły format albo klucz nie pasuje do certyfikatu.", ex);
        }
    }

    private static string DecodeBase64(string value, string purpose, string what)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value.Trim()));
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Wartość {what} ({purpose}) nie jest poprawnym base64.", ex);
        }
    }
}
