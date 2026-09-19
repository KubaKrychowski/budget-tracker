using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Identity.Services.Users;

/// <summary>
/// Nadawanie roli administratora kontom z listy <c>Admin:Emails</c>. Wołane przy starcie serwera (konta, które już
/// istnieją) i po potwierdzeniu adresu e-mail (konta założone później).
/// </summary>
public sealed class AdminRoleService(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<AdminOptions> options,
    ILogger<AdminRoleService> logger)
{
    public async Task EnsureRoleAsync()
    {
        if (await roleManager.RoleExistsAsync(IdentityRoles.Admin)) return;

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>(IdentityRoles.Admin));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Nie udało się utworzyć roli admin: " + string.Join("; ", result.Errors.Select(e => e.Code)));
        }
    }

    public async Task GrantConfiguredAdminsAsync()
    {
        await EnsureRoleAsync();

        foreach (var email in options.Value.Emails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null) await GrantIfConfiguredAsync(user);
        }
    }

    /// <summary>Nadaje rolę, jeśli adres jest na liście i został potwierdzony. Powtarzalne.</summary>
    public async Task GrantIfConfiguredAsync(ApplicationUser user)
    {
        if (!options.Value.IsConfiguredAdmin(user.Email) || !user.EmailConfirmed) return;

        await EnsureRoleAsync();
        if (await userManager.IsInRoleAsync(user, IdentityRoles.Admin)) return;

        var result = await userManager.AddToRoleAsync(user, IdentityRoles.Admin);
        if (result.Succeeded) logger.LogInformation("Nadano rolę admin kontu {UserId}.", user.Id);
    }
}
