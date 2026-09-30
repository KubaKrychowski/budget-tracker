using BudgetTracker.Identity.Controllers;
using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Options;
using BudgetTracker.Identity.Services.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Publiczny formularz prośby o dostęp do bety to jedyny anonimowy zapis w tym serwisie. Testy pilnują decyzji
/// podejmowanych PRZED bazą: co odrzucamy, czego nie zapisujemy i czego nie zdradzamy odpowiedzią.
/// </summary>
/// <remarks>
/// Jak w <see cref="BetaInviteTests"/>: kontekst wskazuje port, na którym nic nie słucha, więc test, w którym kontroler
/// miał NIE dotknąć bazy, a dotknął, pada na połączeniu zamiast po cichu przejść.
/// </remarks>
public sealed class BetaRequestsTests
{
    private static BetaRequestsController Controller()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2;Command Timeout=2")
            .Options);
        var invites = new BetaInviteService(
            db, TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(new RegistrationOptions { ClosedBeta = true }),
            Microsoft.Extensions.Options.Options.Create(new AdminOptions()));

        return new BetaRequestsController(
            new BetaAccessRequestService(db, TimeProvider.System, invites), NullLogger<BetaRequestsController>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("to-nie-jest-adres")]
    [InlineData("a@")]
    public async Task An_invalid_address_is_rejected_before_anything_is_stored(string? email)
    {
        var result = await Controller().Submit(new BetaRequestDto(email, Consent: true, Website: null), default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task An_address_that_is_too_long_is_rejected()
    {
        // Kolumna ma 256 znaków — dłuższy adres wywaliłby zapis 500-tką zamiast zwykłej odmowy.
        var email = new string('a', 250) + "@example.com";

        var result = await Controller().Submit(new BetaRequestDto(email, Consent: true, Website: null), default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Without_consent_nothing_is_stored()
    {
        // ⚠️ To jest cała podstawa prawna zapisu: adres bez zgody nie może trafić do bazy, choćby był poprawny.
        var result = await Controller().Submit(new BetaRequestDto("jan@example.com", Consent: false, Website: null), default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("{ error = consent_required }", bad.Value!.ToString());
    }

    [Fact]
    public async Task A_filled_honeypot_looks_like_success_but_never_touches_the_database()
    {
        // Automatowi nie mówimy, że go rozpoznaliśmy — a baza (port bez serwera) rzuciłaby, gdyby została zapytana.
        var result = await Controller().Submit(new BetaRequestDto("bot@example.com", Consent: true, Website: "http://spam"), default);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task A_missing_body_is_a_bad_request()
    {
        var result = await Controller().Submit(null, default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void The_consent_version_is_a_date_that_matches_the_published_documents()
    {
        // Zgoda zapisana pod wersją to jedyny dowód, na jaki tekst użytkownik się zgodził. Wersja to data w formacie
        // rrrr-mm-dd, tak samo jak „Obowiązuje od" w regulaminie i polityce na landingu.
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", BetaAccessRequestService.ConsentVersion);
    }
}
