using System.ComponentModel.DataAnnotations;
using BudgetTracker.Identity.Services.Api;

namespace BudgetTracker.Identity.Models;

/// <summary>Komunikat po operacji administracyjnej (przekierowanie z POST), pokazywany na górze listy.</summary>
public sealed record AdminFlash(string Text, bool IsError);

/// <summary>Jedno konto na liście użytkowników.</summary>
public sealed record AdminUserRow(
    Guid Id,
    string Email,
    bool IsAdmin,
    bool IsSelf,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    bool IsLockedOut,
    OwnerDataCounts? Data);

public sealed class AdminUsersViewModel
{
    public required IReadOnlyList<AdminUserRow> Users { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int Total { get; init; }

    /// <summary><c>null</c>, gdy API nie odpowiedziało — zakładka pokazuje wtedy tytuł bez liczby.</summary>
    public int? OrphanOwnersCount { get; init; }

    /// <summary><c>false</c>, gdy API nie odpowiedziało: kolumna „Dane w aplikacji" pokazuje kreski, a usuwanie jest ostrzeżone.</summary>
    public bool DataAvailable { get; init; }

    public AdminFlash? Flash { get; init; }

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page * PageSize < Total;

    public int Shown => Users.Count;
}

public sealed class DeleteUserViewModel
{
    public Guid Id { get; set; }

    public string Email { get; set; } = "";

    /// <summary>Dane, które znikną razem z kontem; <c>null</c>, gdy API nie odpowiada.</summary>
    public OwnerDataCounts? Data { get; set; }

    [Required(ErrorMessage = "Validation_EmailRequired")]
    public string ConfirmEmail { get; set; } = "";
}

public sealed class AdminOrphansViewModel
{
    public required IReadOnlyList<OrphanedOwner> Owners { get; init; }

    public int UserCount { get; init; }

    public bool DataAvailable { get; init; }

    public AdminFlash? Flash { get; init; }
}

/// <summary>Konto do wyboru jako docelowe przy przepisywaniu danych.</summary>
public sealed record AccountOption(Guid Id, string Email);

public sealed class AssignOrphansViewModel
{
    public Guid OwnerId { get; set; }

    public OwnerDataCounts Counts { get; set; } = OwnerDataCounts.Empty;

    public IReadOnlyList<AccountOption> Accounts { get; set; } = [];

    [Required(ErrorMessage = "Admin_TargetRequired")]
    public Guid? TargetUserId { get; set; }
}

public sealed class DeleteOrphansViewModel
{
    public Guid OwnerId { get; set; }

    public OwnerDataCounts Counts { get; set; } = OwnerDataCounts.Empty;

    /// <summary>Pierwsze 8 znaków identyfikatora właściciela — tyle trzeba wpisać, żeby potwierdzić.</summary>
    public string ExpectedConfirmation => OwnerId.ToString()[..8];

    [Required(ErrorMessage = "Admin_ConfirmationRequired")]
    public string Confirmation { get; set; } = "";
}

/// <summary>Nagłówek wspólny dla list administratora: tytuł, zakładki z liczbami i komunikat po operacji.</summary>
public sealed record AdminHeaderModel(
    string TitleKey, string SubtitleKey, bool UsersTabActive, int UsersCount, int? OrphansCount, AdminFlash? Flash);
