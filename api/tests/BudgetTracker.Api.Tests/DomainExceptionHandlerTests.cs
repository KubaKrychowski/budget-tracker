using System.Globalization;
using System.Text.Json;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Mapowanie wyjątków na kody HTTP. Odkąd robi to middleware, a nie <c>try/catch</c> przy
/// endpointach, ta tablica jest JEDYNYM miejscem, w którym zapisano „nieznany BusinessId = 404" —
/// więc musi mieć test.
///
/// Bez bazy: handler nie dotyka danych.
/// </summary>
public sealed class DomainExceptionHandlerTests
{
    // AddLogging jest wymagane: fabryka lokalizatorów zasobów bierze ILoggerFactory
    // w konstruktorze, więc bez tego GetRequiredService rzuca przy pierwszym komunikacie.
    private readonly IServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddLocalization()
        // Handler dokłada techniczny `detail` WYŁĄCZNIE poza produkcją, więc pyta o środowisko.
        // Testy jadą jako „Production": sprawdzamy to, co zobaczy obcy, a nie tryb diagnostyczny.
        .AddSingleton<IHostEnvironment>(new FakeEnvironment("Production"))
        .BuildServiceProvider();

    /// <summary>Atrapa środowiska — jedyne, co handler z niego czyta, to nazwa.</summary>
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "testy";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private async Task<(int Status, string? Error)> HandleAsync(Exception exception) =>
        await HandleAsync(exception, _services);

    private static async Task<(int Status, string? Error)> HandleAsync(
        Exception exception, IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        var body = new MemoryStream();
        context.Response.Body = body;

        var handled = await new DomainExceptionHandler(NullLogger<DomainExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, default);

        if (!handled) return (0, null);

        body.Position = 0;
        var text = await new StreamReader(body).ReadToEndAsync();
        var error = text.Length == 0
            ? null
            : JsonDocument.Parse(text).RootElement.GetProperty("error").GetString();

        return (context.Response.StatusCode, error);
    }

    [Fact]
    public async Task Unknown_business_id_becomes_404()
    {
        var (status, error) = await HandleAsync(new BudgetNotFoundException(Guid.NewGuid()));

        Assert.Equal(StatusCodes.Status404NotFound, status);
        // Bez treści — przy 404 nie ma czego napisać poza samym kodem.
        Assert.Null(error);
    }

    [Fact]
    public async Task Disabled_budget_becomes_409_not_400()
    {
        // 409, bo żądanie jest poprawne — to stan zasobu na nie nie pozwala.
        var (status, error) = await HandleAsync(new BudgetDisabledException(Guid.NewGuid()));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task Empty_name_becomes_400_with_a_message_from_resources()
    {
        // Kultura ustawiana jawnie: w produkcji robi to UseRequestLocalization, a tutaj
        // sprawdzamy przy okazji, że komunikat naprawdę idzie przez .resx — gdyby siedział
        // w kodzie wyjątku, przełączenie kultury nic by nie zmieniło.
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("pl");

        try
        {
            var (status, error) = await HandleAsync(new BudgetNameRequiredException());

            Assert.Equal(StatusCodes.Status400BadRequest, status);
            Assert.Equal("Podaj nazwę budżetu.", error);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task The_same_message_falls_back_to_the_neutral_resource()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        try
        {
            var (_, error) = await HandleAsync(new BudgetNameRequiredException());
            Assert.Equal("Give the budget a name.", error);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task Broken_request_body_keeps_its_own_400()
    {
        // Regresja: samo dołożenie UseExceptionHandler przechwytuje ten wyjątek, zanim host
        // zdąży odczytać jego status — bez jawnej gałęzi zepsuty JSON dawał 500.
        var (status, _) = await HandleAsync(new BadHttpRequestException("zepsuty JSON", 400));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Fact]
    public async Task Anything_else_is_left_alone()
    {
        // Handler MUSI przepuszczać nieznane wyjątki, inaczej prawdziwa awaria udawałaby
        // ładną odpowiedź 4xx i nikt by się o niej nie dowiedział.
        var (status, _) = await HandleAsync(new InvalidOperationException("awaria"));

        Assert.Equal(0, status);
    }

    [Fact]
    public async Task Broken_request_body_says_WHY__not_just_400()
    {
        // Regresja z realnego użycia: front dostawał 400 z PUSTYM ciałem, więc mógł napisać
        // tylko „nie udało się połączyć z serwerem" — czyli nieprawdę, bo serwer odpowiedział
        // i to on wiedział, co jest nie tak. Ten sam wyjątek leci przy niepoprawnym parametrze
        // w adresie (`?from=training` nie wiąże się do `DateOnly`), a nie tylko przy złym JSON-ie.
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("pl");

        try
        {
            var (status, error) = await HandleAsync(new BadHttpRequestException("Failed to bind parameter", 400));

            Assert.Equal(StatusCodes.Status400BadRequest, status);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task Other_codes_from_the_same_exception_stay_without_a_message()
    {
        // 413 („żądanie za duże") to ten sam typ wyjątku, ale zdanie o filtrach w adresie
        // byłoby tam nieprawdą — więc treści nie ma.
        var (status, error) = await HandleAsync(new BadHttpRequestException("za duże", 413));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, status);
        Assert.Null(error);
    }

    [Fact]
    public async Task Technical_detail_never_leaves_production()
    {
        // `detail` niesie komunikat frameworka: nietłumaczony i odsłaniający sygnatury metod.
        // Bezcenny przy diagnozie, bezużyteczny i gadatliwy dla obcego.
        var context = new DefaultHttpContext { RequestServices = _services };
        var body = new MemoryStream();
        context.Response.Body = body;

        await new DomainExceptionHandler(NullLogger<DomainExceptionHandler>.Instance)
            .TryHandleAsync(context, new BadHttpRequestException("Failed to bind parameter \"from\"", 400), default);

        body.Position = 0;
        var text = await new StreamReader(body).ReadToEndAsync();

        Assert.DoesNotContain("Failed to bind", text);
    }

    [Fact]
    public async Task Technical_detail_IS_there_in_development()
    {
        // Odwrotna strona tej samej reguły — inaczej „tylko poza produkcją" znaczyłoby
        // „nigdzie", a diagnoza dalej wymagałaby wchodzenia w logi serwera.
        var services = new ServiceCollection()
            .AddLogging()
            .AddLocalization()
            .AddSingleton<IHostEnvironment>(new FakeEnvironment("Development"))
            .BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        var body = new MemoryStream();
        context.Response.Body = body;

        await new DomainExceptionHandler(NullLogger<DomainExceptionHandler>.Instance)
            .TryHandleAsync(context, new BadHttpRequestException("Failed to bind parameter \"from\"", 400), default);

        body.Position = 0;
        var text = await new StreamReader(body).ReadToEndAsync();

        Assert.Contains("Failed to bind", text);
    }

}
