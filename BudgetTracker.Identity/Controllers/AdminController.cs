using System.ComponentModel.DataAnnotations;
using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Resources;
using BudgetTracker.Identity.Services.Api;
using BudgetTracker.Identity.Services.Audit;
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
    BetaInviteService invites,
    IAdminAuditLog audit,
    IAdminAuditReader auditReader,
    IStringLocalizer<SharedResource> localizer,
    ILogger<AdminController> logger) : Controller
{
    private const int PageSize = 20;

    /// <summary>Wpisy historii są krótkie i jest ich dużo, więc strona jest większa niż lista kont.</summary>
    private const int HistoryPageSize = 50;

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
            InvitesCount = await invites.CountAsync(ct),
            OrphanOwnersCount = orphans?.Count,
            HistoryCount = await auditReader.CountAsync(ct),
            DataAvailable = summaries is not null,
            Flash = ReadFlash(),
        });
    }

    /// <summary>
    /// Lista adresów, którym wolno założyć konto w zamkniętej becie, razem z formularzem dopisania kolejnego.
    /// </summary>
    /// <remarks>
    /// Ekran świadomie NIE woła API budżetu: zaproszenie nie ma jeszcze żadnych danych, a przy niedostępnym API
    /// zapraszanie ma działać dalej. Dlatego zakładka „Dane bez właściciela" jest tu bez liczby.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Invites(int page = 1, CancellationToken ct = default)
    {
        var total = await invites.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)PageSize)));

        var rows = await invites.PageAsync(page, PageSize, ct);

        // Które z zaproszonych adresów mają już konto — po to, żeby ekran nie sugerował, że usunięcie wpisu
        // odbiera komuś dostęp. Pytamy tylko o adresy z tej strony listy, nie o całą tabelę kont.
        var emails = rows.Select(i => i.Email).ToList();
        var registered = (await userManager.Users
                .Where(u => u.Email != null && emails.Contains(u.Email.ToLower()))
                .Select(u => u.Email!)
                .ToListAsync(ct))
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        return View(new AdminInvitesViewModel
        {
            Invites = rows.Select(i => new AdminInviteRow(i.Id, i.Email, i.AddedAt, registered.Contains(i.Email))).ToList(),
            Page = page,
            PageSize = PageSize,
            Total = total,
            UsersCount = await userManager.Users.CountAsync(ct),
            HistoryCount = await auditReader.CountAsync(ct),
            ClosedBeta = invites.ClosedBeta,
            Flash = ReadFlash(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInvite(string? email, CancellationToken ct)
    {
        // Walidacja jest tu ręczna, a nie atrybutami na modelu: formularz ma jedno pole i wraca przekierowaniem
        // z komunikatem (jak reszta operacji na tym ekranie), więc osobny model tylko po to, żeby go odesłać
        // z powrotem do widoku, byłby rusztowaniem bez zastosowania.
        if (string.IsNullOrWhiteSpace(email))
        {
            return FlashAndRedirect(nameof(Invites), "Validation_EmailRequired", isError: true);
        }

        if (!new EmailAddressAttribute().IsValid(email.Trim()))
        {
            return FlashAndRedirect(nameof(Invites), "Validation_EmailInvalid", isError: true);
        }

        var (outcome, invite) = await invites.AddAsync(email, CurrentUserId(), ct);
        if (outcome == InviteOutcome.AlreadyInvited)
        {
            return FlashAndRedirect(nameof(Invites), "Admin_Flash_InviteExists", isError: true, invite!.Email);
        }

        logger.LogInformation("Dopisano adres do listy zaproszeń (wpis {InviteId}), wykonał {ActorId}.", invite!.Id, CurrentUserId());
        await audit.RecordAsync(new AdminAuditRecord(
            CurrentUserId(), AdminAuditAction.BetaInviteAdded, AdminAuditOutcome.Succeeded,
            invite.Id, SubjectEmail: invite.Email), ct);

        return FlashAndRedirect(nameof(Invites), "Admin_Flash_InviteAdded", isError: false, invite.Email);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveInvite(Guid id, CancellationToken ct)
    {
        var (outcome, invite) = await invites.RemoveAsync(id, ct);
        if (outcome == InviteOutcome.NotFound)
        {
            // Ślad po próbie też jest śladem: wpis mógł zniknąć, bo usunął go ktoś inny w tej samej chwili.
            await audit.RecordAsync(new AdminAuditRecord(
                CurrentUserId(), AdminAuditAction.BetaInviteRemoved, AdminAuditOutcome.NotFound, id), ct);
            return FlashAndRedirect(nameof(Invites), "Admin_Flash_InviteNotFound", isError: true);
        }

        logger.LogInformation("Usunięto zaproszenie {InviteId}, wykonał {ActorId}.", id, CurrentUserId());
        await audit.RecordAsync(new AdminAuditRecord(
            CurrentUserId(), AdminAuditAction.BetaInviteRemoved, AdminAuditOutcome.Succeeded,
            id, SubjectEmail: invite!.Email), ct);

        return FlashAndRedirect(nameof(Invites), "Admin_Flash_InviteRemoved", isError: false, invite.Email);
    }

    /// <summary>
    /// Dziennik audytu (tylko odczyt). Świadomie NIE woła API budżetu: to ekran, na który zagląda się właśnie wtedy, gdy
    /// coś poszło nie tak (także z API), więc zakładka „Dane bez właściciela" jest tu bez liczby.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> History(int page = 1, CancellationToken ct = default)
    {
        var total = await auditReader.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)HistoryPageSize)));

        var entries = await auditReader.PageAsync(page, HistoryPageSize, ct);

        // Adres wykonawcy pokazujemy, dopóki jego konto istnieje; po usunięciu zostaje sam identyfikator.
        var actorIds = entries.Select(e => e.ActorId).Distinct().ToList();
        var actorEmails = (await userManager.Users.Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email, u.UserName }).ToListAsync(ct))
            .ToDictionary(u => u.Id, u => u.Email ?? u.UserName ?? "");

        return View(new AdminHistoryViewModel
        {
            Rows = entries.Select(e => AdminHistoryRow.From(e, actorEmails)).ToList(),
            Page = page,
            PageSize = HistoryPageSize,
            Total = total,
            UsersCount = await userManager.Users.CountAsync(ct),
            InvitesCount = await invites.CountAsync(ct),
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
            InvitesCount = await invites.CountAsync(ct),
            HistoryCount = await auditReader.CountAsync(ct),
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

        int rows;
        try
        {
            rows = (await data.ReassignAsync(ownerId, target, ct)).Total;
        }
        catch (UserDataServiceException ex)
        {
            logger.LogWarning(ex, "Nie udało się przepisać danych właściciela {OwnerId} (próbował {ActorId}).", ownerId, CurrentUserId());
            await audit.RecordAsync(new AdminAuditRecord(
                CurrentUserId(), AdminAuditAction.OrphanDataAssigned, AdminAuditOutcome.DataServiceUnavailable,
                ownerId, TargetId: target), ct);
            return FlashAndRedirect(nameof(Orphans), "Admin_Flash_DataUnavailable", isError: true);
        }

        logger.LogInformation("Przepisano dane właściciela {OwnerId} na {TargetId} ({Rows} wierszy), wykonał {ActorId}.", ownerId, target, rows, CurrentUserId());
        await audit.RecordAsync(new AdminAuditRecord(
            CurrentUserId(), AdminAuditAction.OrphanDataAssigned, AdminAuditOutcome.Succeeded,
            ownerId, TargetId: target, Rows: rows), ct);

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

        int rows;
        try
        {
            rows = (await data.DeleteDataAsync(ownerId, ct)).Total;
        }
        catch (UserDataServiceException ex)
        {
            logger.LogWarning(ex, "Nie udało się usunąć danych właściciela {OwnerId} (próbował {ActorId}).", ownerId, CurrentUserId());
            await audit.RecordAsync(new AdminAuditRecord(
                CurrentUserId(), AdminAuditAction.OrphanDataDeleted, AdminAuditOutcome.DataServiceUnavailable, ownerId), ct);
            return FlashAndRedirect(nameof(Orphans), "Admin_Flash_DataUnavailable", isError: true);
        }

        logger.LogInformation("Usunięto dane właściciela {OwnerId} ({Rows} wierszy), wykonał {ActorId}.", ownerId, rows, CurrentUserId());
        await audit.RecordAsync(new AdminAuditRecord(
            CurrentUserId(), AdminAuditAction.OrphanDataDeleted, AdminAuditOutcome.Succeeded, ownerId, Rows: rows), ct);

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
    /// <summary>
    /// Zakłada magazyn plików konta, gdy zabrakło go przy rejestracji.
    /// </summary>
    /// <remarks>
    /// ⚠️ Rejestracja NIE przerywa się, gdy API akurat nie odpowiada — konto powstaje mimo to, więc musi
    /// istnieć droga, którą da się to dokończyć później. To jest ta droga. Powtórzenie jest bezpieczne:
    /// API zakłada kontener tylko wtedy, gdy go nie ma.
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStorage(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByGuidAsync(id, ct);
        if (user is null) return FlashAndRedirect(nameof(Users), "Admin_Flash_NotFound", isError: true);

        try
        {
            await data.CreateUserContainerAsync(id, ct);
        }
        catch (UserDataServiceException ex)
        {
            logger.LogWarning(ex, "Nie udało się założyć magazynu plików dla konta {UserId}.", id);
            return FlashAndRedirect(nameof(Users), "Admin_Flash_DataUnavailable", isError: true);
        }

        return FlashAndRedirect(nameof(Users), "Admin_Flash_StorageCreated", isError: false, user.Email ?? user.UserName ?? "");
    }

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
