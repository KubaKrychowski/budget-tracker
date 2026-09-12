using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Budgets.Exceptions;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// „Na których budżetach pracuje ten ekran" — jedna reguła dla całej aplikacji.
/// </summary>
/// <remarks>
/// Wyciągnięte, bo ta sama logika stała już w trzech slice'ach (Dashboard, Transactions,
/// Savings), a CLAUDE.md §10 wskazuje trzecie powtórzenie jako moment na wspólną domenę.
/// Rozjazd między nimi byłby błędem, którego nie widać: dwa ekrany pokazywałyby liczby
/// z RÓŻNYCH budżetów, każdy z nich poprawnie sam w sobie.
///
/// ⚠️ Sama reguła, bez zapytania. Każdy slice pobiera budżety własnym <c>Select</c>-em (bo
/// potrzebuje innych pól), a tutaj przychodzi już tylko uporządkowana lista kandydatów.
/// Dzięki temu wspólny kod nie narzuca nikomu kształtu zapytania.
/// </remarks>
public static class BudgetScope
{
    /// <summary>
    /// Budżet domyślny: najnowszy, który zdążył się zacząć przed datą odniesienia; gdy żaden
    /// nie jest wystarczająco stary — najstarszy istniejący. <c>null</c>, gdy nie ma żadnego.
    /// </summary>
    /// <param name="ordered">Kandydaci POSORTOWANI malejąco po miesiącu (i po Id przy remisie).</param>
    public static Guid? Default(IReadOnlyList<BudgetCandidate> ordered, DateOnly reference)
    {
        if (ordered.Count == 0) return null;

        foreach (var candidate in ordered)
        {
            if (candidate.Month <= reference) return candidate.BusinessId;
        }

        return ordered[^1].BusinessId;
    }

    /// <summary>
    /// Które budżety liczyć. Puste żądanie = jeden domyślny; wskazanie = dokładnie te wskazane.
    /// </summary>
    /// <remarks>
    /// ⚠️ Nieznany identyfikator to <see cref="BudgetNotFoundException"/>, nigdy ciche pominięcie
    /// ani fallback na domyślny. Literówka w adresie pokazywałaby liczby innego budżetu jako swoje,
    /// a przy WIELU identyfikatorach pominięcie jednego jest jeszcze gorsze: odpowiedź wygląda
    /// na kompletną, tylko brakuje w niej pieniędzy.
    ///
    /// Powtórzony identyfikator w adresie nie może liczyć tych samych transakcji dwa razy — stąd
    /// <c>Distinct</c> na wyniku.
    /// </remarks>
    public static IReadOnlyList<Guid> Resolve(
        IReadOnlyList<BudgetCandidate> ordered,
        IReadOnlyList<Guid>? requested,
        DateOnly reference)
    {
        if (ordered.Count == 0) return [];

        if (requested is not { Count: > 0 })
        {
            return Default(ordered, reference) is { } fallback ? [fallback] : [];
        }

        var known = ordered.Select(b => b.BusinessId).ToHashSet();
        foreach (var id in requested)
        {
            if (!known.Contains(id)) throw new BudgetNotFoundException(id);
        }

        return [.. requested.Distinct()];
    }
}
