using BudgetTracker.Identity.Models;
using Microsoft.AspNetCore.Identity;

namespace BudgetTracker.Identity.Services;

/// <summary>
/// Zakłada konto deweloperskie z GÓRY NARZUCONYM identyfikatorem — musi pasować do <c>Budget.UserId</c> zaseedowanego
/// w API, więc nie może dostać losowego <see cref="Guid"/> z domyślnego <c>CreateAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// Hasło NIE ma defaultu w kodzie ani w appsettings (trafiłoby do gita) — pochodzi z konfiguracji
/// (<c>dotnet user-secrets set "Seed:DevPassword" "..." --project BudgetTracker.Identity</c>).
/// </para>
/// <para>
/// ⚠️ Brak hasła albo hasło odrzucone przez politykę haseł to OSTRZEŻENIE, nie wyjątek: serwer tożsamości ma wstać, żeby
/// działało logowanie na pozostałe konta i ekrany administratora. Konto wtedy po prostu nie powstaje (nie zakładamy go bez
/// hasła — kolejny start z poprawnym hasłem zobaczyłby istniejące konto i nigdy go nie ustawił).
/// Konto, które już istnieje, nie potrzebuje hasła i nie daje żadnego ostrzeżenia.
/// </para>
/// </remarks>
public sealed class FixedAccountSeeder(IConfiguration configuration, ILogger<FixedAccountSeeder> logger)
{
    /// <param name="passwordKey">Klucz konfiguracji z hasłem, np. <c>Seed:DevPassword</c>.</param>
    /// <returns><c>true</c>, gdy konto istnieje po wywołaniu (już było albo zostało założone).</returns>
    public async Task<bool> SeedAsync(UserManager<ApplicationUser> userManager, Guid id, string email, string passwordKey)
    {
        if (await userManager.FindByIdAsync(id.ToString()) is not null) return true;

        var password = configuration[passwordKey];
        if (string.IsNullOrEmpty(password))
        {
            logger.LogWarning(
                "Nie ustawiono {PasswordKey} — konto {Email} nie zostało założone, więc nie da się na nie zalogować, a dane z seedu " +
                "API przypisane do tego konta są niedostępne. Ustaw hasło: {Command}",
                passwordKey, email, SetSecretCommand(passwordKey));
            return false;
        }

        var user = new ApplicationUser { Id = id, UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded) return true;

        // W logu tylko opisy błędów polityki (np. „hasło musi zawierać znak specjalny"), nigdy samo hasło.
        logger.LogWarning(
            "Nie udało się założyć konta {Email} z hasłem z {PasswordKey}: {Errors} Konto nie zostało założone, więc nie da się na nie " +
            "zalogować. Ustaw poprawne hasło: {Command}",
            email, passwordKey, string.Join(", ", result.Errors.Select(e => e.Description)), SetSecretCommand(passwordKey));
        return false;
    }

    private static string SetSecretCommand(string passwordKey) =>
        $"dotnet user-secrets set \"{passwordKey}\" \"...\" --project BudgetTracker.Identity";
}
