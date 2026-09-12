using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Savings.Consts;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Queries;

/// <summary>
/// Cel oszczędnościowy i dowód jego odporności.
/// </summary>
/// <remarks>
/// Ekran istnieje po to, żeby udowodnić JEDNO zdanie: odłożyłeś tyle a tyle <b>mimo</b>
/// jednorazowego wydatku. Same liczby tego nie robią — „odłożyłeś 1 500 zł" to informacja,
/// „odłożyłeś 1 500 zł w miesiącu z dużym jednorazowym wydatkiem" to argument.
/// </remarks>
public sealed class GetSavingsQueryHandler(AppDbContext db, SavingsBudgetScope scope, SavingsCategory savingsCategory)
{
    /// <summary>Propozycja podniesienia celu wymaga DWÓCH dowodów — jeden to jeszcze nie wzorzec.</summary>
    private const int SuggestionMinProofs = 2;

    /// <summary>Historia miesięcy z werdyktami, aktywny cel i propozycja jego podniesienia.</summary>
    /// <remarks>
    /// ⚠️ Bieżący miesiąc MUSI być w historii, także pusty. Miesiące biorą się z transakcji,
    /// więc miesiąc bez ani jednej wypadałby z listy — a wtedy ani tabela, ani wykres nie
    /// pokazałyby świeżo ustawionego celu, bo obowiązuje on dopiero od teraz. Użytkownik
    /// zmieniłby kwotę i nie zobaczył po niej ŻADNEGO śladu.
    /// </remarks>
    public async Task<SavingsResponseDto> HandleAsync(IReadOnlyList<Guid>? budgetIds, CancellationToken ct)
    {
        var today = scope.Today();
        var currentMonth = SavingsMonths.FirstDayOf(today);

        var selected = await scope.ResolveAsync(budgetIds, today, ct);
        if (selected.Count == 0)
        {
            return new SavingsResponseDto(
                null, [], EmptyMonth(currentMonth, null), 0, 0m, null, false, false, []);
        }

        var goals = await db.SavingsGoals
            .Where(g => selected.Contains(g.BudgetBusinessId))
            .OrderBy(g => g.StartedOn).ThenBy(g => g.Id)
            .ToListAsync(ct);

        var monthly = await MonthlyTotalsAsync(selected, await savingsCategory.IdAsync(ct), ct);

        var months = monthly
            .Select(m => Judge(m, GoalFor(goals, m.Month)))
            .OrderByDescending(m => m.Month)
            .ToList();

        if (months.Count > 0 && months[0].Month < currentMonth)
        {
            months.Insert(0, EmptyMonth(currentMonth, GoalFor(goals, currentMonth)));
        }

        var active = goals.FirstOrDefault(g => g.EndedOn is null);

        var current = months.FirstOrDefault(m => m.Month == currentMonth)
            ?? EmptyMonth(currentMonth, GoalFor(goals, currentMonth));

        var proofs = months.Where(m => m.Verdict == MonthVerdict.Proof).ToList();

        return new SavingsResponseDto(
            Goal: active is null ? null : new SavingsGoalResponseDto(active.BusinessId, active.Amount, active.StartedOn),
            Months: months,
            CurrentMonth: current,
            ProofCount: proofs.Count,
            DepositedThisYear: months.Where(m => m.Month.Year == today.Year).Sum(m => m.Deposited),
            RaiseSuggestion: Suggest(proofs, active),
            HasAnyLargeExpense: months.Any(m => m.OneOffCount > 0),
            HasAnySavings: months.Any(m => m.Deposited > 0),
            SelectedBudgetIds: selected);
    }

    /// <summary>
    /// Werdykt miesiąca. Trzy warunki, w tej kolejności — i kolejność jest tu regułą, nie stylem.
    /// </summary>
    /// <remarks>
    /// ⚠️ Porównanie idzie po <b>wpłatach</b>, nie po netto. Netto karałoby za normalne używanie
    /// oszczędności: miesiąc z wpłatą 1 500 i wypłatą 1 800 na ubezpieczenie wyszedłby jako
    /// „cel nieosiągnięty", choć odłożone zostało dokładnie tyle, ile trzeba. Aplikacja nie
    /// odróżni automatycznie przelewu tam i z powrotem od uzasadnionej wypłaty, więc wypłata
    /// jedzie obok jako osobna liczba — kombinowanie staje się WIDOCZNE, zamiast być blokowane
    /// regułą, która uderza też w uczciwy przypadek.
    ///
    /// Cel osiągnięty w SPOKOJNYM miesiącu jest miłą wiadomością, ale niczego nie dowodzi —
    /// dowód wymaga OBU warunków naraz.
    /// </remarks>
    private static SavingsMonthResponseDto Judge(MonthlyTotals totals, decimal? goal)
    {
        var verdict = goal is not { } target ? MonthVerdict.NoGoal
            : totals.Deposited < target ? MonthVerdict.GoalMissed
            : totals.OneOffCount == 0 ? MonthVerdict.GoalMet
            : MonthVerdict.Proof;

        return new SavingsMonthResponseDto(
            totals.Month, totals.Deposited, totals.Withdrawn, goal,
            totals.OneOffTotal, totals.OneOffCount, verdict);
    }

    /// <summary>
    /// Cel obowiązujący w danym miesiącu — z TAMTEGO czasu, nie dzisiejszy.
    /// </summary>
    /// <remarks>
    /// Bez tego historia dowodów odnosiłaby się do kwoty, której już nie ma: miesiąc zaliczony
    /// przy celu 1 200 zł zamieniłby się wstecz w porażkę po podniesieniu celu do 1 500 zł.
    /// To jest też powód, dla którego próg na wykresie musi być schodkiem, a nie prostą.
    /// </remarks>
    private static decimal? GoalFor(IReadOnlyList<SavingsGoal> goals, DateOnly month) => goals
        .Where(g => g.StartedOn <= month && (g.EndedOn is null || g.EndedOn >= month))
        .Select(g => (decimal?)g.Amount)
        .LastOrDefault();

    /// <summary>
    /// Propozycja podniesienia celu — jedyna wypowiedź tego ekranu o przyszłości.
    /// </summary>
    /// <remarks>
    /// ⚠️ Zapas to <b>minimum</b> jednorazowych wydatków z miesięcy-dowodów, nie średnia.
    /// Średnia obiecywałaby zapas, którego w najgorszym z tych miesięcy nie było — a to właśnie
    /// najgorszy miesiąc rozstrzyga, czy podniesiony cel się utrzyma.
    /// </remarks>
    private static RaiseGoalSuggestionResponseDto? Suggest(IReadOnlyList<SavingsMonthResponseDto> proofs, SavingsGoal? active)
    {
        if (active is null || proofs.Count < SuggestionMinProofs) return null;

        var headroom = proofs.Min(p => p.OneOffTotal);
        return headroom <= 0 ? null : new RaiseGoalSuggestionResponseDto(headroom, active.Amount + headroom);
    }

    /// <summary>Sumy miesięczne jednym zapytaniem — grupowanie po miesiącu kalendarzowym daty.</summary>
    /// <remarks>
    /// ⚠️ Sumy w zapytaniu zostają ZE ZNAKIEM. Negacja musi być poza zapytaniem — EF nie tłumaczy
    /// <c>-g.Sum(...)</c> wewnątrz projekcji (ta sama pułapka co w <c>GetDashboardQueryHandler</c>).
    /// Przelew na oszczędnościowe WYCHODZI z konta bieżącego, więc w bazie jest ujemny —
    /// na ekranie „odłożone" to wartość dodatnia.
    /// </remarks>
    private async Task<List<MonthlyTotals>> MonthlyTotalsAsync(
        IReadOnlyList<Guid> budgetIds, int? savingsCategoryId, CancellationToken ct)
    {
        var rows = await db.Transactions
            .Where(t => t.BudgetBusinessId != null && budgetIds.Contains(t.BudgetBusinessId.Value))
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Deposited = g.Where(t => t.CategoryId == savingsCategoryId && t.Amount < 0)
                    .Sum(t => (decimal?)t.Amount) ?? 0m,
                Withdrawn = g.Where(t => t.CategoryId == savingsCategoryId && t.Amount > 0)
                    .Sum(t => (decimal?)t.Amount) ?? 0m,
                OneOff = g.Where(t => t.IsLargeExpense && t.Amount < 0)
                    .Sum(t => (decimal?)t.Amount) ?? 0m,
                OneOffCount = g.Count(t => t.IsLargeExpense && t.Amount < 0),
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new MonthlyTotals(
            new DateOnly(r.Year, r.Month, 1),
            -r.Deposited,
            r.Withdrawn,
            -r.OneOff,
            r.OneOffCount))];
    }

    private static SavingsMonthResponseDto EmptyMonth(DateOnly month, decimal? goal) =>
        Judge(new MonthlyTotals(month, 0m, 0m, 0m, 0), goal);

    /// <summary>Sumy jednego miesiąca, już z dodatnimi wpłatami i wydatkami jednorazowymi.</summary>
    private sealed record MonthlyTotals(
        DateOnly Month, decimal Deposited, decimal Withdrawn, decimal OneOffTotal, int OneOffCount);
}
