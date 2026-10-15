using System.Runtime.CompilerServices;
using BudgetTracker.Identity.Services.Users;
using Xunit;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Wersja zgody zapisywana przy prośbie o dostęp musi być wersją dokumentów, które Użytkownik faktycznie widział.
/// </summary>
/// <remarks>
/// ⚠️ Stała i strony HTML żyją w różnych projektach, więc zmiana jednej bez drugiej przechodzi kompilację i nic nie wywala — dopiero
/// później „zgodziłem się” wskazuje na tekst, którego nikt nie pokazywał. Test czyta strony z repozytorium (bez bazy i bez sieci).
/// </remarks>
public sealed class ConsentVersionTests
{
    /// <summary>Szuka od katalogu pliku źródłowego testu, nie od katalogu wyjściowego — ten bywa poza repozytorium (budowanie do %TEMP%).</summary>
    private static string LandingFile(string name, [CallerFilePath] string sourceFile = "")
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "web", "projects", "landing", "public", name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }

        throw new FileNotFoundException($"Nie znaleziono {name} w web/projects/landing/public — test uruchomiono poza repozytorium?");
    }

    [Theory]
    [InlineData("regulamin.html")]
    [InlineData("polityka-prywatnosci.html")]
    public void The_stored_consent_version_is_the_version_printed_on_the_legal_page(string page)
    {
        Assert.Contains($"wersja {BetaAccessRequestService.ConsentVersion}", LandingFile(page));
    }
}
