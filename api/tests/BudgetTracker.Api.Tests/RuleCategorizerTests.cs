using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Testy na PRODUKCYJNYM zestawie reguł z <see cref="BaselineSeed"/>, nie na atrapach.
/// Chodzi o to, żeby wyłapać błędy w samych regułach — zwłaszcza w ich KOLEJNOŚCI,
/// bo wzorce nachodzą na siebie i zła kolejność daje ciche pomyłki.
///
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class RuleCategorizerTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_rules_test;Username=budget;Password=budget_dev_only";

    private AppDbContext _db = null!;
    private RuleCategorizer _categorizer = null!;
    private Dictionary<int, string> _categoryNames = null!;

    public async Task InitializeAsync()
    {
        // Interceptor tez w tescie — tak samo jak w produkcji. Odpowiada wylacznie za zamiane
        // fizycznego kasowania na logiczne; BusinessId nadaje sobie sama encja.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(TimeProvider.System))
            .Options;
        _db = new AppDbContext(options);
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();

        await BaselineSeed.SeedAsync(_db);

        _categoryNames = await _db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name);
        _categorizer = new RuleCategorizer(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private async Task<string?> Categorize(string description, decimal amount = -100m, string type = "Obciążenie")
    {
        var s = await _categorizer.CategorizeAsync(
            DescriptionNormalizer.Normalize(description), type, amount, default);
        return s.CategoryId is { } id ? _categoryNames[id] : null;
    }

    [Theory]
    [InlineData("JMP S.A. BIEDRONKA 4821", "Jedzenie")]
    [InlineData("AUCHAN POLSKA SP. Z", "Jedzenie")]
    [InlineData("ZABKA Z3489 K.2", "Jedzenie")]
    [InlineData("NETFLIX.COM", "Subskrypcje")]
    [InlineData("TAURON SPRZEDAZ", "Rachunki")]
    [InlineData("APTEKA GEMINI", "Zdrowie")]
    [InlineData("CZYNSZ ZA MIESZKANIE", "Mieszkanie")]
    public async Task Matches_well_known_merchants(string description, string expected)
    {
        Assert.Equal(expected, await Categorize(description));
    }

    [Fact]
    public async Task Penalty_wins_over_insurance_despite_overlapping_pattern()
    {
        // "ubezpieczeniowy fundusz" zawiera "ubezpiecz". Gdyby Ubezpieczenia byly sprawdzane
        // pierwsze, kara z UFG wyladowalaby jako skladka — 3550 zl w zlej kategorii.
        Assert.Equal("Mandaty", await Categorize("OPL UBEZPIECZENIOWY FUNDUSZ GWARANCYJNY"));
        Assert.Equal("Ubezpieczenia", await Categorize("POLISAONLINE.ERGOHESTIA.PL"));
    }

    [Theory]
    [InlineData(-11.49, "Gastronomia")]   // hot dog
    [InlineData(-49.99, "Gastronomia")]   // tuz pod progiem
    [InlineData(-50.00, "Paliwo")]        // prog wlaczajaco
    [InlineData(-250.00, "Paliwo")]       // tankowanie
    public async Task Petrol_station_splits_on_amount_not_on_name(decimal amount, string expected)
    {
        // Ta sama nazwa sprzedawcy, dwie rozne kategorie — jedyny sygnal to KWOTA.
        Assert.Equal(expected, await Categorize("ORLEN STACJA NR 0421 GDANSK", amount));
    }

    [Fact]
    public async Task Food_covers_shops_only_restaurants_go_to_gastronomy()
    {
        Assert.Equal("Jedzenie", await Categorize("LIDL SP Z O O"));
        Assert.Equal("Gastronomia", await Categorize("PIZZERIA POD DEBEM"));
        Assert.Equal("Gastronomia", await Categorize("PYSZNE.PL WARSZAWA"));
    }

    [Fact]
    public async Task Catering_matches_both_the_domain_form_and_the_plain_name()
    {
        // Sprzedawca raz jako domena, raz jako nazwa — normalizacja zjada kropkę, reguła ma złapać oba.
        // ⚠️ Wyłącznie sieci z BaselineSeed. Wcześniej stał tu konkretny lokalny catering, czyli
        // test trzymał w repozytorium nazwę firmy z prywatnego wyciągu (issue #13).
        Assert.Equal("Catering", await Categorize("MACZFIT.PL"));
        Assert.Equal("Catering", await Categorize("DIETLY SP Z O O"));
    }

    [Fact]
    public async Task Returns_none_for_unknown_merchant()
    {
        // Nierozpoznane MUSI zwrocic brak, a nie zgadywac — inaczej transakcja nie trafi
        // do przegladu i uzytkownik nigdy jej nie poprawi.
        var s = await _categorizer.CategorizeAsync(
            DescriptionNormalizer.Normalize("XYZ NIEZNANY SPRZEDAWCA"), "Obciążenie", -42m, default);

        Assert.Null(s.CategoryId);
        Assert.Null(s.Confidence);
    }

    [Fact]
    public async Task Rule_match_reports_full_confidence()
    {
        var s = await _categorizer.CategorizeAsync(
            DescriptionNormalizer.Normalize("JMP S.A. BIEDRONKA 4821"), "Obciążenie", -50m, default);

        // Dopasowanie wzorca nie jest predykcja — nie ma tu miejsca na watpliwosc,
        // wiec transakcja nie powinna trafic do przegladu przez niski prog.
        Assert.Equal(1.0m, s.Confidence);
    }

    // ── Wplywy ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Wynagrodzenie 04 2026 SD WORX POLAND SP. Z O.O.", "Przelew na konto", "Wynagrodzenie")]
    [InlineData("00000087208284799 Adres: bolt.eu", "Zwrot w terminalu", "Zwroty")]
    [InlineData("010061097 744636 Adres: RTVEUROAGD", "Zwrot płatności kartą", "Zwroty")]
    [InlineData("ADAM K DOMINIK PALUSZAK", "Przelew na telefon przychodz. zew.", "Przychody inne")]
    public async Task Routes_income_by_its_own_rules(string description, string type, string expected)
    {
        Assert.Equal(expected, await Categorize(description, amount: 1234m, type: type));
    }

    [Fact]
    public async Task Expense_rules_never_fire_on_income()
    {
        // Regresja z realnego wyciagu: wsrod regul wydatkowych sa wzorce na nazwy sklepow,
        // a zwrot ze sklepu ma ten sklep w opisie. Bez bramki na kierunek zwrot pieniedzy
        // zostalby zaksiegowany jako kolejny zakup.
        var s = await _categorizer.CategorizeAsync(
            DescriptionNormalizer.Normalize("ZWROT ZA BIEDRONKA JMP S.A."), "Zwrot w terminalu", 39.90m, default);

        Assert.Equal("Zwroty", _categoryNames[s.CategoryId!.Value]);
    }

    [Fact]
    public async Task Income_rules_never_fire_on_expenses()
    {
        // Przelew WYCHODZACY nie moze wpasc do „Przychodow innych" tylko dlatego,
        // ze typ operacji zaczyna sie od „Przelew".
        Assert.NotEqual("Przychody inne",
            await Categorize("JAN KOWALSKI", amount: -300m, type: "Przelew na telefon"));
    }

    // ── Typ operacji zamiast opisu ─────────────────────────────────────────────────────

    [Fact]
    public async Task Cash_withdrawal_is_matched_by_transaction_type()
    {
        // Opisem wyplaty jest adres bankomatu — tekstowo nie do odroznienia od zakupu.
        Assert.Equal("Gotówka",
            await Categorize("00000012345678901 OBCY UL. LIPOWA 5", -400m, "Wypłata w bankomacie - kod mobilny"));
    }

    [Fact]
    public async Task Cash_withdrawal_wins_over_the_shop_named_in_the_atm_address()
    {
        // Bankomat stojacy w sklepie: gdyby zadecydowal opis, wyplata stalaby sie zakupami.
        // Dlatego regula na typ ma nizszy priorytet niz wszystkie reguly opisowe.
        Assert.Equal("Gotówka",
            await Categorize("BANKOMAT PRZY BIEDRONKA 4821", -500m, "Wypłata w bankomacie - kod mobilny"));
    }
}
