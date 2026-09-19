using BudgetTracker.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Identity.Infrastructure;

public static class UserManagerExtensions
{
    /// <summary>
    /// <c>UserManager.FindByIdAsync</c> przyjmuje <c>string</c> nawet przy kluczu typu <see cref="Guid"/>
    /// (kontrakt <c>IUserStore</c>), więc każdy wywołujący musiałby robić <c>ToString()</c>. Ta wersja
    /// zostawia typ klucza w spokoju.
    /// </summary>
    public static Task<ApplicationUser?> FindByGuidAsync(
        this UserManager<ApplicationUser> userManager, Guid id, CancellationToken ct = default) =>
        userManager.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
}
