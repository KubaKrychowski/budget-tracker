using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace BudgetTracker.Api.Infrastructure.Jobs;

/// <summary>
/// Wydłuża retencję historii przebiegów zadań do <see cref="JobsOptions.HistoryRetentionDays"/>.
/// </summary>
/// <remarks>
/// Hangfire ustawia czas wygaśnięcia przy przejściu zadania w stan końcowy (udane, usunięte). Filtr nadpisuje go dla
/// każdego zadania, więc nowy proces cykliczny dostaje tę samą retencję bez dopisywania czegokolwiek.
/// </remarks>
public sealed class JobHistoryRetentionFilter(TimeSpan retention) : JobFilterAttribute, IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        context.JobExpirationTimeout = retention;
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }
}
