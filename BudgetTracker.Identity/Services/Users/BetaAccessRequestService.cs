using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Identity.Services.Users;

/// <summary>Wynik operacji na prośbie o dostęp.</summary>
public enum RequestOutcome
{
    Created,

    /// <summary>Adres już czeka albo już został zaproszony — nic nie dopisano.</summary>
    AlreadyRequested,

    Invited,

    /// <summary>Adres jest już na liście zaproszeń (dopisany ręcznie) — prośbę tylko oznaczono jako zaproszoną.</summary>
    AlreadyInvited,

    NotFound,

    Removed,
}

/// <summary>Prośby o dostęp do bety zostawione na landingu: zapis z formularza i obsługa ekranu „Prośby o dostęp".</summary>
public sealed class BetaAccessRequestService(ApplicationDbContext db, TimeProvider clock, BetaInviteService invites)
{
    /// <summary>
    /// Wersja Regulaminu i Polityki prywatności na landingu. ⚠️ Podbij ją RAZEM ze zmianą treści dokumentów
    /// (<c>web/projects/landing/public/regulamin.html</c>, <c>polityka-prywatnosci.html</c>) — zgoda zapisana pod starą
    /// wersją dotyczy starego tekstu, i po to ją w ogóle zapisujemy.
    /// </summary>
    public const string ConsentVersion = "2026-09-30";

    /// <summary>
    /// Zapisuje prośbę. Powtórzenie adresu NIE jest błędem i nie różni się od pierwszego zapisu dla wołającego.
    /// </summary>
    /// <remarks>
    /// ⚠️ Endpoint jest publiczny, więc odpowiedź nie może zdradzać, czy adres już jest na liście — inaczej formularz
    /// służyłby do sprawdzania, czyjego adresu tu szukać. Różnicę widzi tylko wynik zwracany do kontrolera (do logu).
    /// </remarks>
    public async Task<RequestOutcome> SubmitAsync(string email, CancellationToken ct)
    {
        var normalized = BetaInvite.Normalize(email);

        if (await db.BetaAccessRequests.AnyAsync(r => r.Email == normalized, ct)) return RequestOutcome.AlreadyRequested;

        db.BetaAccessRequests.Add(new BetaAccessRequest
        {
            Email = normalized,
            RequestedAt = clock.GetUtcNow(),
            ConsentVersion = ConsentVersion,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dwa równoległe zgłoszenia tego samego adresu: oba przeczytały pustkę, unikalny indeks przepuścił jedno.
            return RequestOutcome.AlreadyRequested;
        }

        return RequestOutcome.Created;
    }

    public Task<int> CountAsync(CancellationToken ct) => db.BetaAccessRequests.CountAsync(ct);

    /// <summary>Strona listy: najpierw czekające, w obrębie grup od najnowszych — nowa prośba ma być na górze.</summary>
    public async Task<IReadOnlyList<BetaAccessRequest>> PageAsync(int page, int pageSize, CancellationToken ct) =>
        await db.BetaAccessRequests
            .OrderBy(r => r.InvitedAt != null)
            .ThenByDescending(r => r.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    /// <summary>
    /// Zaprasza adres z prośby: dopisuje go do listy zaproszeń i oznacza prośbę. Powtórzenie jest bezpieczne.
    /// </summary>
    public async Task<(RequestOutcome Outcome, BetaAccessRequest? Request)> InviteAsync(Guid id, Guid actorId, CancellationToken ct)
    {
        var request = await db.BetaAccessRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return (RequestOutcome.NotFound, null);

        var (inviteOutcome, _) = await invites.AddAsync(request.Email, actorId, ct);
        request.InvitedAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return (inviteOutcome == InviteOutcome.AlreadyInvited ? RequestOutcome.AlreadyInvited : RequestOutcome.Invited, request);
    }

    /// <summary>Usuwa prośbę (cofnięcie zgody, koniec bety). ⚠️ NIE usuwa zaproszenia ani konta, jeśli już powstały.</summary>
    public async Task<(RequestOutcome Outcome, BetaAccessRequest? Request)> RemoveAsync(Guid id, CancellationToken ct)
    {
        var request = await db.BetaAccessRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return (RequestOutcome.NotFound, null);

        db.BetaAccessRequests.Remove(request);
        await db.SaveChangesAsync(ct);

        return (RequestOutcome.Removed, request);
    }
}
