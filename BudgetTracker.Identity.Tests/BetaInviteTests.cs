using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Options;
using BudgetTracker.Identity.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Lista zaproszeń do zamkniętej bety decyduje o tym, KTO może w ogóle założyć konto. Pomyłka w jedną stronę
/// wpuszcza obcych, w drugą zamyka drzwi właścicielowi serwera — i żadnej z nich nie złapie kompilator.
/// </summary>
/// <remarks>
/// Testy nie potrzebują bazy, bo sprawdzają decyzje podejmowane PRZED zapytaniem. Kontekst celowo wskazuje port,
/// na którym nic nie słucha: jeśli sprawdzenie miało nie pytać bazy, a jednak pyta, test padnie na połączeniu
/// zamiast po cichu przejść. To jest tu asercja, nie sztuczka — patrz test o pustej liście.
/// </remarks>
public sealed class BetaInviteTests
{
    private static BetaInviteService Service(bool closedBeta, params string[] adminEmails) => new(
        new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2;Command Timeout=2")
            .Options),
        TimeProvider.System,
        Microsoft.Extensions.Options.Options.Create(new RegistrationOptions { ClosedBeta = closedBeta }),
        Microsoft.Extensions.Options.Options.Create(new AdminOptions { Emails = adminEmails }));

    [Theory]
    [InlineData("Jan@Example.COM", "jan@example.com")]
    [InlineData("  jan@example.com  ", "jan@example.com")]
    [InlineData("jan@example.com", "jan@example.com")]
    public void An_invited_address_is_stored_in_one_shape_regardless_of_how_it_was_typed(string typed, string stored)
    {
        // Bez tego zaproszenie wpisane „Jan@Example.COM" nie zadziałałoby dla kogoś, kto w rejestracji
        // wpisze swój adres małymi literami — czyli dla większości zaproszonych.
        Assert.Equal(stored, BetaInvite.Normalize(typed));
    }

    [Fact]
    public void Missing_configuration_means_closed_registration_not_open()
    {
        // ⚠️ To jest cała reguła bezpieczeństwa tej funkcji. Gdyby domyślną wartością było „otwarte", pominięcie
        // sekcji Registration w konfiguracji wdrożenia po cichu wpuściłoby każdego, kto zna adres serwera —
        // i nic by tego nie pokazało, bo ekran rejestracji wygląda tak samo w obu stanach.
        Assert.True(new RegistrationOptions().ClosedBeta);
    }

    [Fact]
    public async Task With_the_closed_beta_off_every_address_may_register()
    {
        Assert.True(await Service(closedBeta: false).IsAllowedToRegisterAsync("ktokolwiek@example.com", default));
    }

    [Fact]
    public async Task A_configured_admin_may_register_even_with_an_empty_invite_list()
    {
        // ⚠️ Bez tego świeża instalacja jest zamknięta także dla osoby, która ma ją skonfigurować: zaproszenia
        // dopisuje się z panelu, do którego trzeba wejść kontem, którego nie dałoby się założyć. Sprawdzenie
        // kończy się PRZED zapytaniem do bazy — gdyby pytało, ten test padłby na nieistniejącym połączeniu.
        Assert.True(await Service(closedBeta: true, "admin@example.com").IsAllowedToRegisterAsync("ADMIN@example.com", default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_address_is_refused_without_asking_the_database(string? email)
    {
        Assert.False(await Service(closedBeta: true, "admin@example.com").IsAllowedToRegisterAsync(email, default));
    }

    [Fact]
    public async Task An_address_outside_the_admin_list_is_checked_against_the_invite_list_and_never_waved_through()
    {
        // Odwrotność poprzednich testów: zwykły adres MUSI trafić do bazy. Gdyby sprawdzenie zwracało „true"
        // przy niedostępnej liście (albo pomijało ją przez pomyłkę w warunku), zamknięta beta byłaby otwarta
        // wszędzie tam, gdzie baza chwilowo nie odpowiada. Nieosiągalne połączenie ma więc rzucić, nie wpuścić.
        var service = Service(closedBeta: true, "admin@example.com");

        await Assert.ThrowsAnyAsync<Exception>(
            () => service.IsAllowedToRegisterAsync("obcy@example.com", default));
    }
}
