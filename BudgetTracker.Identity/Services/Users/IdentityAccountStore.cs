using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace BudgetTracker.Identity.Services.Users;

/// <summary>Prawdziwa implementacja <see cref="IAccountStore"/> na ASP.NET Identity i magazynie OpenIddict.</summary>
public sealed class IdentityAccountStore(
    UserManager<ApplicationUser> userManager,
    IOpenIddictTokenManager tokenManager,
    IOpenIddictAuthorizationManager authorizationManager) : IAccountStore
{
    public async Task<AccountInfo?> FindAsync(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByGuidAsync(id, ct);
        return user is null
            ? null
            : new AccountInfo(user.Id, user.Email ?? user.UserName ?? "", await userManager.IsInRoleAsync(user, IdentityRoles.Admin));
    }

    public async Task<int> CountAdminsAsync(CancellationToken ct) =>
        (await userManager.GetUsersInRoleAsync(IdentityRoles.Admin)).Count;

    public async Task LockAsync(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByGuidAsync(id, ct);
        if (user is null) return;

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        // Nowy znacznik bezpieczeństwa unieważnia istniejące ciasteczka logowania przy najbliższej walidacji.
        await userManager.UpdateSecurityStampAsync(user);
    }

    public async Task RevokeSessionsAsync(Guid id, CancellationToken ct)
    {
        var subject = id.ToString();

        await foreach (var token in tokenManager.FindBySubjectAsync(subject, ct))
        {
            await tokenManager.DeleteAsync(token, ct);
        }

        await foreach (var authorization in authorizationManager.FindBySubjectAsync(subject, ct))
        {
            await authorizationManager.DeleteAsync(authorization, ct);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByGuidAsync(id, ct);
        if (user is null) return;

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Nie udało się usunąć konta: " + string.Join("; ", result.Errors.Select(e => e.Code)));
        }
    }
}
