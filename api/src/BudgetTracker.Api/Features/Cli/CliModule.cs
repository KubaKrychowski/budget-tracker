using BudgetTracker.Api.Features.Cli.Contracts;
using BudgetTracker.Api.Features.Cli.Services;

namespace BudgetTracker.Api.Features.Cli;

/// <summary>
/// Jeden endpoint dla wszystkich komend CLI (issue #25) — <c>Program.cs</c> buduje
/// <see cref="CliCommandRegistry"/> wołając <c>Map&lt;Feature&gt;Cli()</c> każdego feature'a (ten sam
/// wzorzec co lista <c>app.Map&lt;Feature&gt;()</c> dla REST) i przekazuje gotowy rejestr tutaj.
/// </summary>
/// <remarks>
/// Bez <c>Add...</c> w stylu pozostałych modułów: <see cref="CliCommandRegistry"/> i
/// <see cref="CliCommandDispatcher"/> to zwykłe obiekty budowane raz przy starcie, nie serwisy
/// per-żądanie — nie ma czego rejestrować w kontenerze DI.
/// </remarks>
public static class CliModule
{
    public static IEndpointRouteBuilder MapCli(this IEndpointRouteBuilder app, CliCommandRegistry registry)
    {
        var dispatcher = new CliCommandDispatcher(registry);

        app.MapPost("/api/cli/execute", async (
                CliExecuteRequestDto request, IServiceProvider services, CancellationToken ct) =>
            await dispatcher.ExecuteAsync(request.Line, services, ct))
            .WithName("ExecuteCli")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
