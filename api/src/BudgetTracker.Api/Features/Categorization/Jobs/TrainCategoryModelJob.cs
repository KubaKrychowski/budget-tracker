using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Infrastructure.Telemetry;
using System.Diagnostics;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetTracker.Api.Features.Categorization.Jobs;

/// <summary>Douczanie modelu: zbiór z DWÓCH źródeł (plik bazowy + poprawki), trening i publikacja wersji.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>⚠️ <b>Własna kolejka z JEDNYM wykonawcą</b> (patrz <c>JobsModule</c>). Trening to najcięższa
/// operacja w aplikacji: przechodzi cały zbiór i uczy model. Domyślny serwer Hangfire ma 20 workerów,
/// więc bez osobnej kolejki dwa treningi poszłyby równolegle i biły się o procesor z importem.</item>
/// <item>⚠️ <b>Bez ponowień.</b> Domyślne ponawianie zamieniłoby nieudany trening w pętlę liczącą to samo
/// jeszcze kilka razy — a to najdroższa rzecz, jaką aplikacja robi. Nieudany trening ma zostać nieudany
/// i czekać na człowieka; historia przebiegów jest audytem, nie kolejką do odklikania.</item>
/// <item>⚠️ <b>Użytkownik z argumentu, nie z danych.</b> W tle nie ma tokenu, a
/// <c>RlsSessionInterceptor</c> ustawia zmienną sesyjną z <see cref="ICurrentUserAccessor"/>. Bez
/// <see cref="BackgroundUser"/> polityki RLS nie pokazałyby ani jednego wiersza i trening „udałby się”
/// na pustym zbiorze, zamiast zgłosić brak danych.</item>
/// <item>Bufor z modelem zwalnia TEN, kto go dostał — trener oddaje go otwartego.</item>
/// <item><see cref="PerformContext"/> wstrzykuje Hangfire w czasie wykonania — stąd bierze się numer
/// zgłoszenia, który ląduje w wierszu wersji i po którym ekran rozpoznaje SWÓJ trening. Przy uruchomieniu
/// z CLI (poza kolejką) jest <c>null</c> i wersja po prostu nie ma zgłoszenia.</item>
/// </list>
/// </remarks>
public sealed class TrainCategoryModelJob(
    TrainingSetBuilder builder, ModelStore store, ILogger<TrainCategoryModelJob>? logger = null)
{
    private readonly ILogger<TrainCategoryModelJob> _logger = logger ?? NullLogger<TrainCategoryModelJob>.Instance;

    public const string QueueName = "training";

    [Queue(QueueName)]
    [AutomaticRetry(Attempts = 0)]
    public async Task<TrainingReportResponseDto> RunAsync(Guid userId, PerformContext? context, CancellationToken ct)
    {
        var jobId = context?.BackgroundJob.Id;

        // Pusty identyfikator to błąd wywołującego (np. zgłoszenie bez zalogowanego konta), a nie „konto bez danych".
        // Bez tej bramki zadanie policzyłoby cały model na zbiorze widzianym przez RLS jako pusty albo cudzy.
        if (userId == Guid.Empty)
        {
            _logger.LogError("Trening modelu odrzucony: zgłoszenie {JobId} bez identyfikatora konta.", jobId);
            throw new ArgumentException("Trening modelu wymaga identyfikatora konta.", nameof(userId));
        }

        using var asUser = BackgroundUser.Use(userId);
        using var activity = AppTelemetry.Source.StartActivity("categorization.training");
        var started = Stopwatch.GetTimestamp();
        var outcome = "failure";
        _logger.LogInformation("Trening modelu: start, konto {UserId}, zgłoszenie {JobId}.", userId, jobId);

        try
        {
            var set = await builder.BuildAsync(ct);
            if (set.Rows.Count == 0) throw new TrainingDataMissingException();

            var reportData = CategoryModelTrainer.Train(set.Rows);

            try
            {
                await store.Publish(reportData, userId, jobId, ct);
            }
            finally
            {
                await reportData.buffer.DisposeAsync();
            }

            _logger.LogInformation(
                "Trening modelu: koniec, konto {UserId}, zgłoszenie {JobId}, {Rows} przykładów, trafność micro {Micro:P1} / macro {Macro:P1}.",
                userId, jobId, reportData.Item1.Rows, reportData.Item1.MicroAccuracy, reportData.Item1.MacroAccuracy);

            outcome = "success";
            activity?.SetTag("training.rows", reportData.Item1.Rows);
            return reportData.Item1;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Zadanie nie ma ponowień, więc ten wpis jest jedynym śladem POWODU (panel Hangfire pokazuje sam wyjątek).
            _logger.LogError(e, "Trening modelu: niepowodzenie, konto {UserId}, zgłoszenie {JobId}.", userId, jobId);
            throw;
        }
        finally
        {
            AppTelemetry.TrainingDuration.Record(
                Stopwatch.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>("outcome", outcome));
        }
    }
}
