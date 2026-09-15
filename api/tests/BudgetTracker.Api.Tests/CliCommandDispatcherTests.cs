using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Contracts;
using BudgetTracker.Api.Features.Cli.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Mechanika wykonania komendy CLI (issue #25): tokenizacja, dopasowanie rzeczownik/czasownik,
/// <c>help</c>, błędy składni. Bez bazy — te reguły nie zależą od handlerów feature'ów, więc
/// rejestrujemy tu tylko atrapy. Wiązanie konkretnych komend z prawdziwymi handlerami sprawdza
/// <see cref="CliIntegrationTests"/>.
/// </summary>
public sealed class CliCommandDispatcherTests
{
    private static CliCommandRegistry Registry()
    {
        var registry = new CliCommandRegistry();

        registry.Register("widget", "echo", "Zwraca to, co dostał.", "widget echo [--text <fraza>]",
            [CliFlag.Optional("text", "Tekst do zwrócenia.")],
            (_, args, _) => Task.FromResult<object?>(new { text = args.GetFlag("text") }));

        registry.Register("widget", "required", "Wymaga flagi.", "widget required --amount <kwota>",
            [CliFlag.Required("amount", "Kwota.")],
            (_, args, _) => Task.FromResult<object?>(new { amount = args.GetRequiredDecimalFlag("amount") }));

        return registry;
    }

    /// <summary>
    /// <c>Results.BadRequest(new { error = ... })</c> instancjuje <c>BadRequest&lt;T&gt;</c> anonimowym typem T,
    /// którego w teście nie da się nazwać — stąd odczyt przez wspólne interfejsy zamiast dopasowania typu.
    /// </summary>
    private static int? StatusOf(IResult result) => (result as IStatusCodeHttpResult)?.StatusCode;

    private static object? BodyOf(IResult result) => (result as IValueHttpResult)?.Value;

    [Fact]
    public async Task Wykonuje_zarejestrowana_komende_z_flaga()
    {
        var dispatcher = new CliCommandDispatcher(Registry());
        var result = await dispatcher.ExecuteAsync(
            "widget echo --text \"cudzysłów ze spacją\"", NullServiceProvider.Instance, CancellationToken.None);

        var body = BodyOf(result);
        Assert.Equal("cudzysłów ze spacją", body?.GetType().GetProperty("text")?.GetValue(body));
    }

    [Fact]
    public async Task Nieznany_rzeczownik_daje_400_z_podpowiedzia_help()
    {
        var dispatcher = new CliCommandDispatcher(Registry());
        var result = await dispatcher.ExecuteAsync("budzet list", NullServiceProvider.Instance, CancellationToken.None);

        Assert.Equal(400, StatusOf(result));
    }

    [Fact]
    public async Task Nieznany_czasownik_wymienia_dostepne_dla_tego_rzeczownika()
    {
        var dispatcher = new CliCommandDispatcher(Registry());
        var result = await dispatcher.ExecuteAsync("widget usun", NullServiceProvider.Instance, CancellationToken.None);

        var body = BodyOf(result);
        var error = (string)body!.GetType().GetProperty("error")!.GetValue(body)!;
        Assert.Contains("echo", error);
        Assert.Contains("required", error);
    }

    [Fact]
    public async Task Brak_wymaganej_flagi_daje_400_zamiast_wyjatku()
    {
        var dispatcher = new CliCommandDispatcher(Registry());
        var result = await dispatcher.ExecuteAsync("widget required", NullServiceProvider.Instance, CancellationToken.None);

        Assert.Equal(400, StatusOf(result));
    }

    [Fact]
    public async Task Wyjatek_spoza_CliArgumentException_leci_dalej_niezlapany()
    {
        // ⚠️ To jest SEDNO architektury: dispatcher NIE łapie wyjątków domenowych — mają trafić
        // do DomainExceptionHandler tym samym pipeline'em co przy REST. Test dowodzi, że nic
        // pomiędzy tym nie stoi.
        var registry = new CliCommandRegistry();
        registry.Register("boom", "now", "Rzuca.", "boom now", [],
            (_, _, _) => throw new InvalidOperationException("boom"));

        var dispatcher = new CliCommandDispatcher(registry);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.ExecuteAsync("boom now", NullServiceProvider.Instance, CancellationToken.None));
    }

    [Fact]
    public async Task Help_bez_argumentow_zwraca_wszystkie_komendy()
    {
        var dispatcher = new CliCommandDispatcher(Registry());
        var result = await dispatcher.ExecuteAsync("help", NullServiceProvider.Instance, CancellationToken.None);

        var commands = Assert.IsAssignableFrom<IReadOnlyList<CliCommandDescriptionDto>>(BodyOf(result));
        Assert.Equal(2, commands.Count);
    }

    [Fact]
    public async Task Help_z_rzeczownikiem_zawęża_liste()
    {
        var dispatcher = new CliCommandDispatcher(Registry());
        var result = await dispatcher.ExecuteAsync("widget help", NullServiceProvider.Instance, CancellationToken.None);

        var commands = Assert.IsAssignableFrom<IReadOnlyList<CliCommandDescriptionDto>>(BodyOf(result));
        Assert.All(commands, c => Assert.Equal("widget", c.Noun));
    }

    [Fact]
    public async Task Pusta_komenda_daje_400()
    {
        var dispatcher = new CliCommandDispatcher(Registry());
        var result = await dispatcher.ExecuteAsync("   ", NullServiceProvider.Instance, CancellationToken.None);

        Assert.Equal(400, StatusOf(result));
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public static readonly NullServiceProvider Instance = new();
        public object? GetService(Type serviceType) => null;
    }
}
