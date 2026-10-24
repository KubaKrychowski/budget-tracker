using BudgetTracker.Api.Infrastructure.Jobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Storage Hangfire'a w pamięci (<see cref="JobsModule"/>). Bez bazy — i o to chodzi: Hangfire nie ma prawa
/// dotykać Postgresa, bo jego odpytywanie nie pozwalało darmowemu Neonowi zasnąć.
/// </summary>
public sealed class JobsStorageTests
{
    /// <summary>
    /// Regresja do uniknięcia: storage w pamięci domyślnie numeruje zgłoszenia od 1 po każdym restarcie, a JobId
    /// treningu leży w <c>ModelVersion</c> i po nim status rozpoznaje „gotowe". Powtórzony numer pokazałby świeżo
    /// zgłoszony trening jako skończony, z raportem starego przebiegu.
    /// </summary>
    [Fact]
    public void Job_ids_are_guids_so_they_never_repeat_after_a_restart()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddJobs(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        var client = new BackgroundJobClient(provider.GetRequiredService<JobStorage>());
        var jobId = client.Create(Job.FromExpression(() => Console.WriteLine()), new ScheduledState(TimeSpan.FromDays(1)));

        Assert.True(Guid.TryParse(jobId, out _), $"Zgłoszenie ma identyfikator '{jobId}', a nie GUID.");
    }
}
