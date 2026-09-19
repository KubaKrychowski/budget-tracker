using BudgetTracker.Identity.Models;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Dziennik nie ma adresów e-mail, więc wiersz historii musi z tego, co ma (identyfikatory, skrót), ułożyć czytelny opis.
/// Testy pilnują reguł wyboru opisu i tego, że adres wykonawcy pojawia się tylko, dopóki jego konto istnieje.
/// </summary>
public sealed class AdminHistoryRowTests
{
    private static readonly Guid Actor = Guid.Parse("01a0b8b6-062e-7ca2-a931-fd0de099539e");
    private static readonly Guid Subject = Guid.Parse("01a0b8b6-0e6a-731e-958f-3379c6757952");
    private static readonly Guid Target = Guid.Parse("01a0b8b6-1543-75aa-93fd-0a722ea8bf36");
    private const string Hash = "f7d54c22dda5b1780276601cd005909b70543fc4a6ba72d735b4cb9bbb611f01";

    private static AdminHistoryRow Row(
        AdminAuditAction action = AdminAuditAction.UserDeleted, AdminAuditOutcome outcome = AdminAuditOutcome.Succeeded,
        Guid? actor = null, Guid? subject = null, string? hash = Hash, Guid? target = null, int? rows = 5) =>
        new(DateTimeOffset.UnixEpoch, actor ?? Actor, ActorEmail: null, action, outcome, subject ?? Subject, hash, target, rows);

    [Fact]
    public void A_known_address_hash_is_shown_shortened_under_the_account_id()
    {
        var row = Row();

        Assert.Equal(AdminHistorySubjectNote.EmailHash, row.SubjectNote);
        Assert.Equal("f7d54c22…", row.HashShort);
    }

    [Fact]
    public void Refusing_to_delete_yourself_is_described_as_the_performers_own_account()
    {
        var row = Row(outcome: AdminAuditOutcome.RefusedSelf, subject: Actor, hash: null, rows: null);

        Assert.Equal(AdminHistorySubjectNote.SubjectIsActor, row.SubjectNote);
    }

    [Fact]
    public void Reassigning_data_shows_the_target_account_not_the_owner_that_has_no_account()
    {
        var row = Row(AdminAuditAction.OrphanDataAssigned, subject: Guid.Empty, hash: null, target: Target);

        Assert.Equal(AdminHistorySubjectNote.AssignedTo, row.SubjectNote);
        Assert.Equal("01a0b8b6-1543-75aa…", row.TargetShort);
    }

    [Theory]
    [InlineData(AdminAuditAction.OrphanDataDeleted)]
    [InlineData(AdminAuditAction.UserDeleted)]
    public void Without_a_hash_a_target_or_the_actor_there_is_nothing_to_describe(AdminAuditAction action)
    {
        var row = Row(action, subject: Guid.CreateVersion7(), hash: null);

        Assert.Equal(AdminHistorySubjectNote.NoAccount, row.SubjectNote);
        Assert.Null(row.HashShort);
        Assert.Null(row.TargetShort);
    }

    [Fact]
    public void Identifiers_are_shortened_to_a_fixed_prefix()
    {
        Assert.Equal("01a0b8b6-062e-7ca2…", Row().ActorShort);
        Assert.Equal("01a0b8b6-0e6a-731e…", Row().SubjectShort);
    }

    [Theory]
    [InlineData(AdminAuditOutcome.Succeeded, "ok")]
    [InlineData(AdminAuditOutcome.RefusedSelf, "warn")]
    [InlineData(AdminAuditOutcome.RefusedLastAdmin, "warn")]
    [InlineData(AdminAuditOutcome.DataServiceUnavailable, "bad")]
    [InlineData(AdminAuditOutcome.NotFound, "")]
    public void Outcome_colour_separates_success_refusal_and_failure(AdminAuditOutcome outcome, string css) =>
        Assert.Equal(css, Row(outcome: outcome).OutcomeCss);

    [Fact]
    public void The_actor_address_comes_only_from_accounts_that_still_exist()
    {
        var entry = new AdminAuditEntry
        {
            Id = Guid.CreateVersion7(), OccurredAt = DateTimeOffset.UnixEpoch, ActorId = Actor,
            Action = AdminAuditAction.UserDeleted, Outcome = AdminAuditOutcome.Succeeded, SubjectId = Subject,
        };

        var existing = AdminHistoryRow.From(entry, new Dictionary<Guid, string> { [Actor] = "jan.kowalski@example.com" });
        var deleted = AdminHistoryRow.From(entry, new Dictionary<Guid, string>());

        Assert.Equal("jan.kowalski@example.com", existing.ActorEmail);
        Assert.Null(deleted.ActorEmail);
    }
}
