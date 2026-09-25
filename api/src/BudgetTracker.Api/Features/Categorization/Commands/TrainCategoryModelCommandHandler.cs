using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Jobs;
using BudgetTracker.Api.Infrastructure;
using Hangfire;

namespace BudgetTracker.Api.Features.Categorization.Commands;

/// <summary>
/// Zgłasza douczanie modelu do kolejki — samo uczenie dzieje się w tle.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>⚠️ <b>Odpowiedź nie niesie metryk</b>, bo w chwili odpowiedzi trening się jeszcze nie zaczął.
/// Ekran poznaje zakończony trening po NOWEJ AKTYWNEJ WERSJI na liście modeli, nie po tej odpowiedzi.</item>
/// <item>Zgłoszenie jest tanie i celowo NIE sprawdza, czy jest z czego uczyć: składanie zbioru czyta bazę
/// i blob, więc trzymałoby żądanie otwarte przez całą tę pracę. Pusty zbiór kończy zadanie błędem
/// widocznym w panelu (<see cref="TrainCategoryModelJob"/>).</item>
/// <item>Właściciel jedzie ARGUMENTEM zadania — w tle nie ma tokenu, a bez identyfikatora polityki RLS
/// nie pokazałyby zadaniu ani jednego wiersza.</item>
/// </list>
/// </remarks>
public sealed class TrainCategoryModelCommandHandler(IBackgroundJobClient jobs, ICurrentUserAccessor currentUser)
{
    public TrainingQueuedResponseDto Handle()
    {
        var userId = currentUser.UserId;
        // PerformContext i token uzupełnia Hangfire przy wykonaniu — w wyrażeniu idą jako null/None.
        var jobId = jobs.Enqueue<TrainCategoryModelJob>(job => job.RunAsync(userId, null!, CancellationToken.None));

        return new TrainingQueuedResponseDto(jobId);
    }
}
