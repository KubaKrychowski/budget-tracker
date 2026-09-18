using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Exceptions;
using BudgetTracker.Api.Features.Savings.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Savings.Commands;

/// <summary>Ustawia albo zmienia cel oszczędnościowy budżetu.</summary>
public sealed class SetSavingsGoalCommandHandler(
    AppDbContext db, SavingsBudgetScope scope, TimeProvider clock, ICurrentUserAccessor currentUser)
{
    /// <summary>
    /// Ustawia albo zmienia cel. Zmiana KOŃCZY poprzedni datą i zakłada nowy — nie nadpisuje.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Od kiedy obowiązuje, decyduje SERWER, i robi to asymetrycznie.</b> Podniesienie
    /// wchodzi od bieżącego miesiąca; obniżenie dopiero od następnego. Powód jest jeden:
    /// obniżenie to jedyny kierunek, którym dałoby się wyprodukować dowód wstecz — wystarczyłoby
    /// 30. listopada zjechać z celem poniżej tego, co się odłożyło, i listopad zamieniłby się
    /// w „dowód odporności". Podnoszenie może dowód najwyżej odebrać, więc nie ma czego pilnować.
    /// </para>
    /// <list type="bullet">
    /// <item>Ta sama kwota to nie zmiana. Zakładanie identycznego celu tylko pociąłoby historię
    /// na kawałki, z których żaden niczego nie zmienia.</item>
    /// <item>PIERWSZY cel wolno zadeklarować wstecz — bez tego cała dotychczasowa historia zostaje
    /// „bez celu" i dowód nie ma jak powstać wcześniej niż za miesiąc (patrz <see cref="SetSavingsGoalRequestDto"/>).
    /// Data jest normalizowana do pierwszego dnia miesiąca: werdykt dotyczy całego miesiąca.</item>
    /// <item>Poprzedni cel, który nie objął ani jednego pełnego miesiąca (ustawiony i zmieniony w tym samym
    /// miesiącu), jest kasowany logicznie zamiast domykany — przedział „od marca do lutego" byłby bez sensu.</item>
    /// </list>
    /// </remarks>
    public async Task<SavingsGoalResponseDto> HandleAsync(SetSavingsGoalRequestDto request, CancellationToken ct)
    {
        if (request.Amount <= 0) throw new SavingsGoalAmountInvalidException();

        var today = scope.Today();
        var currentMonth = SavingsMonths.FirstDayOf(today);

        var budgetId = await scope.SingleAsync(request.BudgetId, today, ct);

        var active = await db.SavingsGoals
            .Where(g => g.BudgetBusinessId == budgetId && g.EndedOn == null)
            .OrderByDescending(g => g.StartedOn).ThenByDescending(g => g.Id)
            .FirstOrDefaultAsync(ct);

        if (active is not null && active.Amount == request.Amount)
        {
            return new SavingsGoalResponseDto(active.BusinessId, active.Amount, active.StartedOn);
        }

        var startsOn = active is null && request.StartedOn is { } requestedStart
            ? SavingsMonths.FirstDayOf(requestedStart)
            : currentMonth;

        if (active is not null)
        {
            var lowering = request.Amount < active.Amount;
            startsOn = lowering ? currentMonth.AddMonths(1) : currentMonth;

            var endsOn = startsOn.AddMonths(-1);
            if (endsOn < active.StartedOn)
            {
                db.SavingsGoals.Remove(active);
            }
            else
            {
                active.End(endsOn);
            }
        }

        var goal = new SavingsGoal(budgetId, request.Amount, startsOn, clock.GetUtcNow(), currentUser.UserId);

        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync(ct);

        return new SavingsGoalResponseDto(goal.BusinessId, goal.Amount, goal.StartedOn);
    }
}
