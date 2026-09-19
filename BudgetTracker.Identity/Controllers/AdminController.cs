using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Resources;
using BudgetTracker.Identity.Services.Api;
using BudgetTracker.Identity.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BudgetTracker.Identity.Controllers;

/// <summary>
/// Zarządzanie użytkownikami: lista kont, trwałe usuwanie razem z danymi oraz dane bez właściciela (makiety w Figmie,
/// strona „Logowanie (Identity)", ramki „Admin — …"). Tylko rola <see cref="IdentityRoles.Admin"/>.
/// </summary>
/// <remarks>
/// Ekrany żyją w Identity, a nie w Angularze: konta to domena serwera tożsamości, dostęp chroni ciasteczko z rolą,
/// więc nie ma nowego REST-a dla przeglądarki. Dane w API budżetu (ilość, kasowanie, przepisanie) idą przez
/// <see cref="IUserDataClient"/> tokenem serwisowym — nigdy tokenem użytkownika.
/// </remarks>
[Authorize(Roles = IdentityRoles.Admin)]
public sealed class AdminController(
    UserManager<ApplicationUser> userManager,
    IUserDataClient data,
    UserDeletionService deletion,
    IStringLocalizer<SharedResource> localizer,
    ILogger<AdminController> logger) : Controller
{
    private const int PageSize = 20;

    /// <summary>Ile znaków identyfikatora właściciela trzeba wpisać, żeby potwierdzić usunięcie jego danych.</summary>
    private const int ConfirmationLength = 8;

    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Users));

    [HttpGet]
    public async Task<IActionResult> Users(int page = 1, CancellationToken ct = default)
    {
        var total = await userManager.Users.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)PageSize)));

        var accounts = await userManager.Users
            .OrderBy(u => u.Email).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        var adminIds = (await userManager.GetUsersInRoleAsync(IdentityRoles.Admin)).Select(u => u.Id).ToHashSet();
        var currentId = CurrentUserId();

        var summaries = await TryAsync(() => data.GetSummariesAsync(accounts.Select(a => a.Id).ToList(), ct));
        var orphans = await TryAsync(async () => await data.GetOrphansAsync(await AllUserIdsAsync(ct), ct));
        var now = DateTimeOffset.UtcNow;

        var rows = accounts.Select(a => new AdminUserRow(
            a.Id,
            a.Email ?? a.UserName ?? "",
            adminIds.Contains(a.Id),
            a.Id == currentId,
            a.EmailConfirmed,
            a.TwoFactorEnabled,
            a.LockoutEnabled && a.LockoutEnd is { } end && end > now,
            summaries?.GetValueOrDefault(a.Id, OwnerDataCounts.Empty))).ToList();

        return View(new AdminUsersViewModel
        {
            Users = rows,
            Page = page,
            PageSize = PageSize,
            Total = total,
            OrphanOwnersCount = orphans?.Count,
            DataAvailable = summaries is not null,
            Flash = ReadFlash(),
        });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByGuidAsync(id, ct);
        if (user is null) return FlashAndRedirect(nameof(Users), "Admin_Flash_NotFound", isError: true);

        var summaries = await TryAsync(() => data.GetSummariesAsync([id], ct));

        return View(new DeleteUserViewModel
        {
            Id = id,
            Email = user.Email ?? user.UserName ?? "",
            Data = summaries?.GetValueOrDefault(id, OwnerDataCounts.Empty),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(Guid id, DeleteUserViewModel model, CancellationToken ct)
    {
        var user = await userManager.FindByGuidAsync(id, ct);
        if (user is null) return FlashAndRedirect(nameof(Users), "Admin_Flash_NotFound", isError: true);

        var email = user.Email ?? user.UserName ?? "";
        model.Id = id;
        model.Email = email;

        if (!string.Equals(model.ConfirmEmail?.Trim(), email, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ConfirmEmail), localizer["Admin_ConfirmMismatch"]);
            model.Data = (await TryAsync(() => data.GetSummariesAsync([id], ct)))?.GetValueOrDefault(id, OwnerDataCounts.Empty);
            return View(model);
        }

        var outcome = await deletion.DeleteByAdminAsync(id, CurrentUserId(), ct);
        return outcome switch
        {
            UserDeletionOutcome.Deleted => FlashAndRedirect(nameof(Users), "Admin_Flash_Deleted", isError: false, email),
            UserDeletionOutcome.NotFound => FlashAndRedirect(nameof(Users), "Admin_Flash_NotFound", isError: true),
            UserDeletionOutcome.CannotDeleteSelf => FlashAndRedirect(nameof(Users), "Admin_Flash_CannotDeleteSelf", isError: true),
            UserDeletionOutcome.LastAdmin => FlashAndRedirect(nameof(Users), "Admin_Flash_LastAdmin", isError: true),
            _ => FlashAndRedirect(nameof(Users), "Admin_Flash_DataUnavailable", isError: true),
        };
    }

    [HttpGet]
    public async Task<IActionResult> Orphans(CancellationToken ct)
    {
        var userIds = await AllUserIdsAsync(ct);
        var owners = await TryAsync(() => data.GetOrphansAsync(userIds, ct));

        return View(new AdminOrphansViewModel
        {
            Owners = owners ?? [],
            UserCount = userIds.Count,
            DataAvailable = owners is not null,
            Flash = ReadFlash(),
        });
    }

    [HttpGet]
    public async Task<IActionResult> AssignOrphans(Guid ownerId, CancellationToken ct)
    {
        var owner = await FindOrphanAsync(ownerId, ct);
        if (owner is null) return FlashAndRedirect(nameof(Orphans), "Admin_Flash_OrphanGone", isError: true);

        return View(await BuildAssignModelAsync(ownerId, owner.Counts, targetUserId: null, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignOrphans(Guid ownerId, AssignOrphansViewModel model, CancellationToken ct)
    {
        var owner = await FindOrphanAsync(ownerId, ct);
        if (owner is null) return FlashAndRedirect(nameof(Orphans), "Admin_Flash_OrphanGone", isError: true);

        if (!ModelState.IsValid || model.TargetUserId is not { } target || await userManager.FindByGuidAsync(target, ct) is null)
        {
            if (ModelState.IsValid) ModelState.AddModelError(nameof(model.TargetUserId), localizer["Admin_TargetRequired"]);
            return View(await BuildAssignModelAsync(ownerId, owner.Counts, model.TargetUserId, ct));
        }

        try
        {
            await data.ReassignAsync(ownerId, target, ct);
        }
        catch (UserDataServiceException ex)
        {
            logger.LogWarning(ex, "Nie udało się przepisać danych właściciela {OwnerId}.", ownerId);
            return FlashAndRedirect(nameof(Orphans), "Admin_Flash_DataUnavailable", isError: true);
        }

        return FlashAndRedirect(nameof(Orphans), "Admin_Flash_Assigned", isError: false);
    }

    [HttpGet]
    public async Task<IActionResult> DeleteOrphans(Guid ownerId, CancellationToken ct)
    {
        var owner = await FindOrphanAsync(ownerId, ct);
        if (owner is null) return FlashAndRedirect(nameof(Orphans), "Admin_Flash_OrphanGone", isError: true);

        return View(new DeleteOrphansViewModel { OwnerId = ownerId, Counts = owner.Counts });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOrphans(Guid ownerId, DeleteOrphansViewModel model, CancellationToken ct)
    {
        // ⚠️ Właściciel MUSI być bez konta: to jest ścieżka dla danych bez właściciela, nie skrót do kasowania danych
        // żywego użytkownika z pominięciem blokady konta i sprawdzeń z UserDeletionService.
        var owner = await FindOrphanAsync(ownerId, ct);
        if (owner is null) return FlashAndRedirect(nameof(Orphans), "Admin_Flash_OrphanGone", isError: true);

        model.OwnerId = ownerId;
        model.Counts = owner.Counts;

        if (!string.Equals(model.Confirmation?.Trim(), model.ExpectedConfirmation, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Confirmation), localizer["Admin_ConfirmMismatch"]);
            return View(model);
        }

        try
        {
            await data.DeleteDataAsync(ownerId, ct);
        }
        catch (UserDataServiceException ex)
        {
            logger.LogWarning(ex, "Nie udało się usunąć danych właściciela {OwnerId}.", ownerId);
            return FlashAndRedirect(nameof(Orphans), "Admin_Flash_DataUnavailable", isError: true);
        }

        return FlashAndRedirect(nameof(Orphans), "Admin_Flash_OrphansDeleted", isError: false, model.ExpectedConfirmation);
    }

    private Guid CurrentUserId() => Guid.Parse(userManager.GetUserId(User)!);

    private async Task<IReadOnlyList<Guid>> AllUserIdsAsync(CancellationToken ct) =>
        await userManager.Users.Select(u => u.Id).ToListAsync(ct);

    /// <summary>Właściciel bez konta o wskazanym identyfikatorze albo <c>null</c>, gdy ma konto, nie ma danych albo API nie odpowiada.</summary>
    private async Task<OrphanedOwner?> FindOrphanAsync(Guid ownerId, CancellationToken ct)
    {
        var owners = await TryAsync(async () => await data.GetOrphansAsync(await AllUserIdsAsync(ct), ct));
        return owners?.FirstOrDefault(o => o.OwnerId == ownerId);
    }

    private async Task<AssignOrphansViewModel> BuildAssignModelAsync(
        Guid ownerId, OwnerDataCounts counts, Guid? targetUserId, CancellationToken ct) => new()
    {
        OwnerId = ownerId,
        Counts = counts,
        TargetUserId = targetUserId,
        Accounts = (await userManager.Users.OrderBy(u => u.Email).ToListAsync(ct))
            .Select(u => new AccountOption(u.Id, u.Email ?? u.UserName ?? "")).ToList(),
    };

    /// <summary>Wywołanie API, które przy awarii daje <c>null</c> zamiast wyjątku — ekran pokazuje wtedy ostrzeżenie.</summary>
    private async Task<T?> TryAsync<T>(Func<Task<T>> call) where T : class
    {
        try
        {
            return await call();
        }
        catch (UserDataServiceException ex)
        {
            logger.LogWarning(ex, "API budżetu nie odpowiedziało.");
            return null;
        }
    }

    private IActionResult FlashAndRedirect(string action, string resourceKey, bool isError, params object[] args)
    {
        TempData[TempDataKeys.AdminFlashText] = localizer[resourceKey, args].Value;
        TempData[TempDataKeys.AdminFlashIsError] = isError;
        return RedirectToAction(action);
    }

    private AdminFlash? ReadFlash() =>
        TempData[TempDataKeys.AdminFlashText] is string text
            ? new AdminFlash(text, TempData[TempDataKeys.AdminFlashIsError] is true)
            : null;
}
