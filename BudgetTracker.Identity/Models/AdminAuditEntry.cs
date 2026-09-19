namespace BudgetTracker.Identity.Models;

/// <summary>
/// Jeden wpis dziennika audytu operacji, które kasują albo przenoszą konta i dane. Dziennik jest tylko do dopisywania —
/// żaden ekran ani kod nie edytuje ani nie usuwa wpisów.
/// </summary>
/// <remarks>
/// ⚠️ Nie ma tu adresu e-mail. Konto po usunięciu przestaje istnieć, a adres to dana osobowa, której nie chcemy trzymać
/// w dzienniku. Zamiast niego jest skrót (<see cref="SubjectEmailHash"/>): pozwala ODNALEŹĆ wpis po znanym adresie,
/// ale nie pokazuje adresu. To pseudonimizacja, nie anonimizacja — skrót adresu z małej, przewidywalnej puli da się
/// odgadnąć słownikiem.
/// </remarks>
public sealed class AdminAuditEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Kto to zrobił: administrator, a przy „Usuń moje konto" właściciel konta.</summary>
    public Guid ActorId { get; init; }

    public AdminAuditAction Action { get; init; }

    public AdminAuditOutcome Outcome { get; init; }

    /// <summary>Konto, którego dotyczy operacja, albo identyfikator właściciela danych bez właściciela.</summary>
    public Guid SubjectId { get; init; }

    /// <summary>SHA-256 znormalizowanego adresu konta (hex, 64 znaki); <c>null</c>, gdy nie ma konta (dane bez właściciela).</summary>
    public string? SubjectEmailHash { get; init; }

    /// <summary>Konto docelowe przy przepisywaniu danych.</summary>
    public Guid? TargetId { get; init; }

    /// <summary>Ile wierszy danych w API zostało usuniętych albo przepisanych; <c>null</c>, gdy nic nie dotarło do API.</summary>
    public int? Rows { get; init; }
}
