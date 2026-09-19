using System.Security.Claims;
using BudgetTracker.Api.Features.Admin;
using BudgetTracker.Api.Features.Admin.Consts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Bariera endpointów <c>/api/admin</c>: kasują i przepisują dane KAŻDEGO konta, więc dostęp ma mieć wyłącznie
/// klient serwisowy serwera tożsamości. Testy sprawdzają samą politykę — bez uruchamiania API i bazy.
/// </summary>
public sealed class AdminPolicyTests
{
    private static readonly IAuthorizationService Authorization = new ServiceCollection()
        .AddLogging()
        .AddAuthorization(AdminModule.ConfigureAdminPolicy)
        .BuildServiceProvider()
        .GetRequiredService<IAuthorizationService>();

    private static ClaimsPrincipal Authenticated(params string[] scopes)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        if (scopes.Length > 0) principal.SetScopes(scopes);
        return principal;
    }

    private static async Task<bool> AllowedAsync(ClaimsPrincipal principal) =>
        (await Authorization.AuthorizeAsync(principal, AdminPolicies.Admin)).Succeeded;

    [Fact]
    public async Task A_service_token_with_the_admin_scope_is_allowed()
    {
        Assert.True(await AllowedAsync(Authenticated(IdentityServerDefaults.AdminScope)));
    }

    [Fact]
    public async Task An_ordinary_user_token_is_rejected_even_with_the_regular_api_scope()
    {
        // Token SPA i bt-cli: openid + profile + email + budgettracker_api — bez zakresu admin.
        Assert.False(await AllowedAsync(Authenticated(
            Scopes.OpenId, Scopes.Profile, Scopes.Email, IdentityServerDefaults.ApiAudience)));
    }

    [Fact]
    public async Task A_role_claim_named_admin_is_not_enough()
    {
        // Rola admin w Identity daje dostęp do EKRANÓW zarządzania, nie do surowego API danych.
        var principal = Authenticated(IdentityServerDefaults.ApiAudience);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(Claims.Role, "admin"));

        Assert.False(await AllowedAsync(principal));
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_rejected()
    {
        Assert.False(await AllowedAsync(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
