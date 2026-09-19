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
}
