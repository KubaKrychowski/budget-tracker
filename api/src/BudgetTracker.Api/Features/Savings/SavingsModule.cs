using BudgetTracker.Api.Features.Cli;
using BudgetTracker.Api.Features.Cli.Services;
using BudgetTracker.Api.Features.Savings.Commands;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Queries;
using BudgetTracker.Api.Features.Savings.Services;

namespace BudgetTracker.Api.Features.Savings;

/// <summary>Rejestracja DI i endpointy celu oszczędnościowego i rezerwacji — Program.cs tylko woła te metody (CLAUDE.md §4).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Endpointy NIE łapią wyjątków — kody rozstrzyga <c>DomainExceptionHandler</c> w jednym miejscu.
/// Stąd <c>Produces</c> jako jedyny ślad po kontrakcie.</item>
/// <item><c>budgetId</c> odczytów jest TABLICĄ i nazwa została w liczbie pojedynczej — tak samo jak na liście
/// transakcji, żeby adres z jednym budżetem znaczył na obu ekranach to samo.</item>
/// <item>Rezygnacja z celu KOŃCZY cel datą, nie kasuje wiersza — stąd 204 i brak treści.</item>
/// <item>Rezerwacje mają osobne żądanie od <c>/api/savings</c>: ekran listy rezerwacji nie potrzebuje historii
/// miesięcy ani werdyktów, a kafel na ekranie oszczędności pobiera obie rzeczy równolegle.</item>
/// <item>Kandydaci do rozliczenia są pobierani DOPIERO przy otwarciu modala, nie razem z listą — inaczej każdy
/// wiersz ciągnąłby własne zapytanie po transakcjach, żeby pokazać nic.</item>
/// </list>
/// </remarks>
public static class SavingsModule
{
    public static IServiceCollection AddSavings(this IServiceCollection services)
    {
        services.AddScoped<SavingsBudgetScope>();
        services.AddScoped<SavingsCategory>();
        services.AddScoped<ReservationLookup>();
        services.AddScoped<SavingsAccount>();
        services.AddScoped<ContributeToReservationCommandHandler>();

        services.AddScoped<SetSavingsGoalCommandHandler>();
        services.AddScoped<EndSavingsGoalCommandHandler>();
        services.AddScoped<CreateSavingsReservationCommandHandler>();
        services.AddScoped<UpdateSavingsReservationCommandHandler>();
        services.AddScoped<DeleteSavingsReservationCommandHandler>();
        services.AddScoped<SettleSavingsReservationCommandHandler>();
        services.AddScoped<UnsettleSavingsReservationCommandHandler>();

        services.AddScoped<GetSavingsQueryHandler>();
        services.AddScoped<GetSavingsReservationsQueryHandler>();
        services.AddScoped<GetSettleCandidatesQueryHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapSavings(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/savings", async (
            Guid[]? budgetId, GetSavingsQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(budgetId, ct)))
            .WithName("GetSavings")
            .Produces<SavingsResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/savings/goal", async (
            SetSavingsGoalRequestDto request, SetSavingsGoalCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("SetSavingsGoal")
            .Produces<SavingsGoalResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/savings/goal", async (
            Guid? budgetId, EndSavingsGoalCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(budgetId, ct);
            return Results.NoContent();
        })
            .WithName("EndSavingsGoal")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/savings/reservations", async (
            Guid[]? budgetId, GetSavingsReservationsQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(budgetId, ct)))
            .WithName("GetSavingsReservations")
            .Produces<SavingsReservationsResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/savings/reservations", async (
            SaveReservationRequestDto request, CreateSavingsReservationCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("CreateSavingsReservation")
            .Produces<SavingsReservationResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/api/savings/reservations/{id:guid}", async (
            Guid id, SaveReservationRequestDto request, UpdateSavingsReservationCommandHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("UpdateSavingsReservation")
            .Produces<SavingsReservationResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/savings/reservations/{id:guid}", async (
            Guid id, DeleteSavingsReservationCommandHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return Results.NoContent();
        })
            .WithName("DeleteSavingsReservation")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/savings/reservations/{id:guid}/settle-candidates", async (
            Guid id, GetSettleCandidatesQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, ct)))
            .WithName("GetSettleCandidates")
            .Produces<IReadOnlyList<SettleCandidateResponseDto>>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/savings/reservations/{id:guid}/settle", async (
            Guid id, SettleReservationRequestDto request, SettleSavingsReservationCommandHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("SettleSavingsReservation")
            .Produces<SavingsReservationResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        app.MapDelete("/api/savings/reservations/{id:guid}/settle", async (
            Guid id, UnsettleSavingsReservationCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, ct)))
            .WithName("UnsettleSavingsReservation")
            .Produces<SavingsReservationResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/savings/reservations/{id:guid}/contributions", async (
            Guid id, ContributeRequestDto request, ContributeToReservationCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.ContributeAsync(id, request, ct)))
            .WithName("ContributeToSavingsReservation")
            .Produces<SavingsReservationResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        app.MapDelete("/api/savings/reservations/{id:guid}/contributions/{contributionId:guid}", async (
            Guid id, Guid contributionId, ContributeToReservationCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.WithdrawAsync(id, contributionId, ct)))
            .WithName("WithdrawSavingsContribution")
            .Produces<SavingsReservationResponseDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>Komendy CLI (issue #25): rzeczownik <c>savings</c> (cel) i <c>reservation</c> (rezerwacje).</summary>
    public static CliCommandRegistry MapSavingsCli(this CliCommandRegistry registry)
    {
        registry.Register("savings", "show", "Stan konta oszczędnościowego, cel i dowód.",
            "savings show [--budget-id <guid,...>]",
            [CliFlag.Optional("budget-id", "Lista BusinessId budżetów po przecinku; puste = budżet domyślny.")],
            async (sp, args, ct) => await sp.GetRequiredService<GetSavingsQueryHandler>()
                .HandleAsync(args.GetGuidArrayFlag("budget-id"), ct));

        registry.Register("savings", "set-goal", "Ustawia albo zmienia cel oszczędnościowy.",
            "savings set-goal --amount <kwota> [--budget-id <guid>] [--started-on <RRRR-MM-01>]",
            [
                CliFlag.Required("amount", "Nowa kwota miesięczna celu."),
                CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego."),
                CliFlag.Optional("started-on",
                    "Od którego miesiąca cel obowiązuje — honorowane TYLKO przy pierwszym celu budżetu."),
            ],
            async (sp, args, ct) =>
            {
                var request = new SetSavingsGoalRequestDto(
                    args.GetRequiredDecimalFlag("amount"), args.GetGuidFlag("budget-id"), args.GetDateFlag("started-on"));
                return await sp.GetRequiredService<SetSavingsGoalCommandHandler>().HandleAsync(request, ct);
            });

        registry.Register("savings", "end-goal", "Rezygnuje z celu (kończy go datą, nie kasuje historii).",
            "savings end-goal [--budget-id <guid>]",
            [CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego.")],
            async (sp, args, ct) =>
            {
                var budgetId = args.GetGuidFlag("budget-id");
                await sp.GetRequiredService<EndSavingsGoalCommandHandler>().HandleAsync(budgetId, ct);
                return new { ended = true };
            });

        registry.Register("reservation", "list", "Rezerwacje na ten rok.",
            "reservation list [--budget-id <guid,...>]",
            [CliFlag.Optional("budget-id", "Lista BusinessId budżetów po przecinku; puste = budżet domyślny.")],
            async (sp, args, ct) => await sp.GetRequiredService<GetSavingsReservationsQueryHandler>()
                .HandleAsync(args.GetGuidArrayFlag("budget-id"), ct));

        registry.Register("reservation", "create", "Zakłada rezerwację.", ReservationUsage("create"), ReservationFlags(),
            async (sp, args, ct) =>
                await sp.GetRequiredService<CreateSavingsReservationCommandHandler>().HandleAsync(ToSaveRequest(args), ct));

        registry.Register("reservation", "update", "Zmienia rezerwację (budżet ignorowany przy zmianie).",
            ReservationUsage("update <id>"), ReservationFlags(),
            async (sp, args, ct) => await sp.GetRequiredService<UpdateSavingsReservationCommandHandler>()
                .HandleAsync(args.GetGuid(0), ToSaveRequest(args), ct));

        registry.Register("reservation", "delete", "Usuwa rezerwację.", "reservation delete <id>", [],
            async (sp, args, ct) =>
            {
                var id = args.GetGuid(0);
                await sp.GetRequiredService<DeleteSavingsReservationCommandHandler>().HandleAsync(id, ct);
                return new { deleted = true, id };
            });

        registry.Register("reservation", "settle-candidates", "Wypłaty z oszczędności, którymi można rozliczyć rezerwację.",
            "reservation settle-candidates <id>", [],
            async (sp, args, ct) =>
                await sp.GetRequiredService<GetSettleCandidatesQueryHandler>().HandleAsync(args.GetGuid(0), ct));

        registry.Register("reservation", "settle", "Rozlicza rezerwację wskazaną wypłatą.",
            "reservation settle <id> --transaction-id <guid>",
            [CliFlag.Required("transaction-id", "Wypłata z oszczędności z „reservation settle-candidates”.")],
            async (sp, args, ct) =>
            {
                var request = new SettleReservationRequestDto(args.GetRequiredGuidFlag("transaction-id"));
                return await sp.GetRequiredService<SettleSavingsReservationCommandHandler>()
                    .HandleAsync(args.GetGuid(0), request, ct);
            });

        registry.Register("reservation", "unsettle", "Cofa rozliczenie rezerwacji.",
            "reservation unsettle <id>", [],
            async (sp, args, ct) =>
                await sp.GetRequiredService<UnsettleSavingsReservationCommandHandler>().HandleAsync(args.GetGuid(0), ct));

        registry.Register("reservation", "contribute", "Wpłaca na cel — kwota umownie odłożona z oszczędności.",
            "reservation contribute <id> --amount <kwota>",
            [CliFlag.Required("amount", "Kwota wpłaty.")],
            async (sp, args, ct) =>
            {
                var request = new ContributeRequestDto(args.GetRequiredDecimalFlag("amount"));
                return await sp.GetRequiredService<ContributeToReservationCommandHandler>()
                    .ContributeAsync(args.GetGuid(0), request, ct);
            });

        registry.Register("reservation", "remove-contribution", "Cofa jedną wpłatę na cel.",
            "reservation remove-contribution <id> <contributionId>", [],
            async (sp, args, ct) => await sp.GetRequiredService<ContributeToReservationCommandHandler>()
                .WithdrawAsync(args.GetGuid(0), args.GetGuid(1), ct));

        return registry;
    }

    private static string ReservationUsage(string verbAndArgs) =>
        $"reservation {verbAndArgs} --name <nazwa> --amount <kwota> [--due-month <RRRR-MM-01>] "
        + "[--priority <n>] [--budget-id <guid>]";

    private static IReadOnlyList<CliFlagDefinition> ReservationFlags() =>
    [
        CliFlag.Required("name", "Nazwa rezerwacji."),
        CliFlag.Required("amount", "Docelowa kwota."),
        CliFlag.Optional("due-month", "Dowolny dzień miesiąca terminu; pomiń dla „przy okazji”."),
        CliFlag.Optional("priority", "Kolejność wśród rezerwacji, domyślnie 0."),
        CliFlag.Optional("budget-id", "BusinessId budżetu; pomiń dla budżetu domyślnego. Przy „update” ignorowany."),
    ];

    private static SaveReservationRequestDto ToSaveRequest(CliArgs args) => new(
        args.GetRequiredFlag("name"),
        args.GetRequiredDecimalFlag("amount"),
        args.GetDateFlag("due-month"),
        args.GetIntFlag("priority", defaultValue: 0),
        args.GetGuidFlag("budget-id"));
}
