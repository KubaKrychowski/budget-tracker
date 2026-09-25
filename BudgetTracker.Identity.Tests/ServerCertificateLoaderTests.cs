using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Options;

namespace BudgetTracker.Identity.Tests;

public sealed class ServerCertificateLoaderTests : IDisposable
{
    private const string Password = "test-only-password";

    private readonly string dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "bt-cert-tests-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private string WritePfx(DateTimeOffset notBefore, DateTimeOffset notAfter, string password = Password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=bt-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(notBefore, notAfter);

        var path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".pfx");
        File.WriteAllBytes(path, cert.Export(X509ContentType.Pfx, password));
        return path;
    }

    [Fact]
    public void Valid_pfx_is_loaded_with_its_private_key()
    {
        var path = WritePfx(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        using var cert = ServerCertificateLoader.Load(new CertificateFileOptions { Path = path, Password = Password }, "podpisujący");

        Assert.True(cert.HasPrivateKey);
        Assert.Equal("CN=bt-test", cert.Subject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_path_fails_and_names_the_certificate_purpose(string? path)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ServerCertificateLoader.Load(new CertificateFileOptions { Path = path }, "podpisujący"));

        Assert.Contains("podpisujący", ex.Message);
        Assert.Contains(ServerCertificatesOptions.SectionName, ex.Message);
    }

    [Fact]
    public void Missing_section_fails_instead_of_starting_without_a_certificate()
    {
        Assert.Throws<InvalidOperationException>(() => ServerCertificateLoader.Load(null, "szyfrujący"));
    }

    [Fact]
    public void Nonexistent_file_fails()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ServerCertificateLoader.Load(new CertificateFileOptions { Path = Path.Combine(dir, "nie-ma.pfx") }, "podpisujący"));

        Assert.Contains("nie-ma.pfx", ex.Message);
    }

    [Fact]
    public void Wrong_password_fails_with_a_readable_message()
    {
        var path = WritePfx(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var ex = Assert.Throws<InvalidOperationException>(
            () => ServerCertificateLoader.Load(new CertificateFileOptions { Path = path, Password = "zle-haslo" }, "podpisujący"));

        Assert.Contains("hasło", ex.Message);
    }

    [Fact]
    public void Expired_certificate_is_rejected()
    {
        var path = WritePfx(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1));

        var ex = Assert.Throws<InvalidOperationException>(
            () => ServerCertificateLoader.Load(new CertificateFileOptions { Path = path, Password = Password }, "podpisujący"));

        Assert.Contains("wygasł", ex.Message);
    }

    [Fact]
    public void File_that_is_not_a_pfx_is_rejected()
    {
        var path = Path.Combine(dir, "smiec.pfx");
        File.WriteAllText(path, "to nie jest certyfikat");

        Assert.Throws<InvalidOperationException>(
            () => ServerCertificateLoader.Load(new CertificateFileOptions { Path = path }, "podpisujący"));
    }

    // ----- Druga droga: para PEM-ów wprost z konfiguracji (hosting bez trwałego miejsca na plik) -----

    private static (string Pem, string Key) MakePem(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=bt-test-pem", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(notBefore, notAfter);

        return (Base64(cert.ExportCertificatePem()), Base64(rsa.ExportPkcs8PrivateKeyPem()));
    }

    private static string Base64(string text) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Valid_pem_pair_is_loaded_with_a_usable_private_key()
    {
        var (pem, key) = MakePem(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        using var cert = ServerCertificateLoader.Load(new CertificateFileOptions { Pem = pem, PemKey = key }, "podpisujący");

        Assert.True(cert.HasPrivateKey);
        Assert.Equal("CN=bt-test-pem", cert.Subject);

        // ⚠️ Sam HasPrivateKey nie wystarcza: CreateFromPem daje klucz efemeryczny, który potrafi przejść ten
        // warunek, a wywalić się dopiero przy PIERWSZYM podpisie — czyli po starcie, przy pierwszym logowaniu.
        // Ten podpis jest tu po to, żeby taka usterka padła w teście, a nie u użytkownika.
        using var rsa = cert.GetRSAPrivateKey();
        Assert.NotNull(rsa);
        Assert.NotEmpty(rsa.SignData([1, 2, 3], HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void Pem_without_a_private_key_fails_and_says_which_setting_is_missing()
    {
        var (pem, _) = MakePem(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var ex = Assert.Throws<InvalidOperationException>(
            () => ServerCertificateLoader.Load(new CertificateFileOptions { Pem = pem }, "szyfrujący"));

        Assert.Contains("PemKey", ex.Message);
        Assert.Contains("szyfrujący", ex.Message);
    }

    [Fact]
    public void Pem_that_is_not_base64_fails_with_a_readable_message()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ServerCertificateLoader.Load(
                new CertificateFileOptions { Pem = "-----BEGIN CERTIFICATE-----", PemKey = "cokolwiek" }, "podpisujący"));

        Assert.Contains("base64", ex.Message);
    }

    [Fact]
    public void Private_key_that_does_not_match_the_certificate_is_rejected()
    {
        var (pem, _) = MakePem(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var (_, foreignKey) = MakePem(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        Assert.Throws<InvalidOperationException>(
            () => ServerCertificateLoader.Load(new CertificateFileOptions { Pem = pem, PemKey = foreignKey }, "podpisujący"));
    }

    [Fact]
    public void Expired_pem_certificate_is_rejected_just_like_an_expired_file()
    {
        var (pem, key) = MakePem(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1));

        var ex = Assert.Throws<InvalidOperationException>(
            () => ServerCertificateLoader.Load(new CertificateFileOptions { Pem = pem, PemKey = key }, "podpisujący"));

        Assert.Contains("wygasł", ex.Message);
    }

    [Fact]
    public void With_both_sources_configured_the_pem_wins_and_the_file_is_not_touched()
    {
        // Rozstrzygnięcie pierwszeństwa jest tu zapisane celowo: przy wdrożeniu, na którym zostało stare
        // ustawienie ze ścieżką, a doszło nowe z PEM-em, serwer ma wziąć to, co świeższe — zamiast wywalić się
        // na nieistniejącym pliku albo, gorzej, wystawiać tokeny podpisane nieaktualnym kluczem.
        var (pem, key) = MakePem(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        using var cert = ServerCertificateLoader.Load(
            new CertificateFileOptions { Pem = pem, PemKey = key, Path = Path.Combine(dir, "nie-ma.pfx") }, "podpisujący");

        Assert.Equal("CN=bt-test-pem", cert.Subject);
    }
}
