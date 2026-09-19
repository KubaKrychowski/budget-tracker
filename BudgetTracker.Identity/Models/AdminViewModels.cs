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

    public int HistoryCount { get; init; }

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

    public int HistoryCount { get; init; }

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

/// <summary>Zakładki ekranów administratora.</summary>
public enum AdminTab
{
    Users,
    Orphans,
    History,
}

/// <summary>Nagłówek wspólny dla list administratora: tytuł, zakładki z liczbami i komunikat po operacji.</summary>
/// <param name="OrphansCount"><c>null</c>, gdy nie znamy liczby (API nie odpowiada albo ekran jej nie potrzebuje) — zakładka jest wtedy bez liczby.</param>
public sealed record AdminHeaderModel(
    string TitleKey, string SubtitleKey, AdminTab Active, int UsersCount, int? OrphansCount, int HistoryCount, AdminFlash? Flash);

/// <summary>Co pokazać pod identyfikatorem konta w historii, skoro adresu e-mail w dzienniku nie ma.</summary>
public enum AdminHistorySubjectNote
{
    /// <summary>Skrót adresu — po nim można sprawdzić, czy to było konto o danym adresie.</summary>
    EmailHash,

    /// <summary>Wykonawca i konto to ta sama osoba (próba usunięcia siebie, „Usuń moje konto").</summary>
    SubjectIsActor,

    /// <summary>Przepisanie danych: pokazujemy konto docelowe.</summary>
    AssignedTo,

    /// <summary>Dane bez właściciela albo konto, którego nie było — nie ma czego opisać.</summary>
    NoAccount,
}

/// <summary>Jeden wpis dziennika audytu w postaci gotowej do wyświetlenia.</summary>
public sealed record AdminHistoryRow(
    DateTimeOffset OccurredAt,
    Guid ActorId,
    string? ActorEmail,
    AdminAuditAction Action,
    AdminAuditOutcome Outcome,
    Guid SubjectId,
    string? SubjectEmailHash,
    Guid? TargetId,
    int? Rows)
{
    /// <summary>Tyle znaków identyfikatora pokazujemy w tabeli; pełny jest w podpowiedzi po najechaniu.</summary>
    private const int ShortIdLength = 18;

    private const int ShortHashLength = 8;

    public string ActionKey => $"Admin_History_Action_{Action}";

    public string OutcomeKey => $"Admin_History_Outcome_{Outcome}";

    /// <summary>Klasa kropki wyniku: zielona = udane, żółta = odmowa, czerwona = awaria, szara = brak konta.</summary>
    public string OutcomeCss => Outcome switch
    {
        AdminAuditOutcome.Succeeded => "ok",
        AdminAuditOutcome.RefusedSelf or AdminAuditOutcome.RefusedLastAdmin => "warn",
        AdminAuditOutcome.DataServiceUnavailable => "bad",
        _ => "",
    };

    public string ActorShort => ShortId(ActorId);

    public string SubjectShort => ShortId(SubjectId);

    public string? TargetShort => TargetId is { } target ? ShortId(target) : null;

    public string? HashShort => SubjectEmailHash is { Length: > 0 } hash ? hash[..Math.Min(ShortHashLength, hash.Length)] + "…" : null;

    public AdminHistorySubjectNote SubjectNote =>
        Action == AdminAuditAction.OrphanDataAssigned && TargetId is not null ? AdminHistorySubjectNote.AssignedTo
        : SubjectEmailHash is not null ? AdminHistorySubjectNote.EmailHash
        : SubjectId == ActorId ? AdminHistorySubjectNote.SubjectIsActor
        : AdminHistorySubjectNote.NoAccount;

    public static AdminHistoryRow From(AdminAuditEntry entry, IReadOnlyDictionary<Guid, string> actorEmails) => new(
        entry.OccurredAt, entry.ActorId, actorEmails.GetValueOrDefault(entry.ActorId),
        entry.Action, entry.Outcome, entry.SubjectId, entry.SubjectEmailHash, entry.TargetId, entry.Rows);

    private static string ShortId(Guid id) => id.ToString()[..ShortIdLength] + "…";
}

public sealed class AdminHistoryViewModel
{
    public required IReadOnlyList<AdminHistoryRow> Rows { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int Total { get; init; }

    public int UsersCount { get; init; }

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page * PageSize < Total;
}
