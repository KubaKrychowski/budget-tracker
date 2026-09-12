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

        return app;
    }
}
