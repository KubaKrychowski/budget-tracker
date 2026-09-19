using BudgetTracker.Api.Features.Admin.Commands;
using BudgetTracker.Api.Features.Admin.Consts;
using BudgetTracker.Api.Features.Admin.Contracts;
using BudgetTracker.Api.Features.Admin.Queries;
using BudgetTracker.Api.Features.Admin.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;

namespace BudgetTracker.Api.Features.Admin;

/// <summary>Rejestracja DI i endpointy administracyjne — wołane WYŁĄCZNIE przez serwer tożsamości (token serwisowy).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Cała grupa wymaga <see cref="AdminPolicies.Admin"/>: zakres <c>budgettracker_admin</c> ma tylko klient
/// serwisowy Identity. Zwykły token użytkownika, także admina z Identity, dostaje tu 403.</item>
/// <item>Endpointy NIE łapią wyjątków — kody rozstrzyga <c>DomainExceptionHandler</c>. Stąd <c>Produces</c>.</item>
/// <item>POST zamiast GET dla list identyfikatorów: lista kont nie mieści się rozsądnie w adresie.</item>
/// </list>
/// </remarks>
public static class AdminModule
{
    /// <summary>
    /// Polityka <see cref="AdminPolicies.Admin"/>: token musi mieć zakres <c>budgettracker_admin</c>. Wydzielona z
    /// <c>Program.cs</c>, żeby dało się ją przetestować bez uruchamiania całego API.
    /// </summary>
    public static void ConfigureAdminPolicy(AuthorizationOptions options) =>
        options.AddPolicy(AdminPolicies.Admin, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.HasScope(IdentityServerDefaults.AdminScope)));

    public static IServiceCollection AddAdmin(this IServiceCollection services)
    {
        services.AddScoped<OwnerDataService>();

        services.AddScoped<GetUserDataSummariesQueryHandler>();
        services.AddScoped<GetOrphanedDataQueryHandler>();
        services.AddScoped<DeleteOwnerDataCommandHandler>();
        services.AddScoped<ReassignOwnerDataCommandHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AdminPolicies.Admin);

        admin.MapPost("/users/data-summary", async (
            UserDataSummaryRequestDto request, GetUserDataSummariesQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("GetUserDataSummaries")
            .Produces<UserDataSummariesResponseDto>()
            .Produces(StatusCodes.Status403Forbidden);

        admin.MapPost("/orphans", async (
            OrphanedDataRequestDto request, GetOrphanedDataQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("GetOrphanedData")
            .Produces<OrphanedDataResponseDto>()
            .Produces(StatusCodes.Status403Forbidden);

        admin.MapDelete("/owners/{ownerId:guid}/data", async (
            Guid ownerId, DeleteOwnerDataCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ownerId, ct)))
            .WithName("DeleteOwnerData")
            .Produces<OwnerDataChangeResponseDto>()
            .Produces(StatusCodes.Status403Forbidden);

        admin.MapPost("/owners/{ownerId:guid}/reassign", async (
            Guid ownerId, ReassignOwnerRequestDto request, ReassignOwnerDataCommandHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ownerId, request, ct)))
            .WithName("ReassignOwnerData")
            .Produces<OwnerDataChangeResponseDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
