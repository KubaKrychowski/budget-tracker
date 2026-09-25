using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Hangfire;
using Hangfire.Server;

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
public sealed class TrainCategoryModelJob(TrainingSetBuilder builder, ModelStore store)
{
    public const string QueueName = "training";

    [Queue(QueueName)]
    [AutomaticRetry(Attempts = 0)]
    public async Task<TrainingReportResponseDto> RunAsync(Guid userId, PerformContext? context, CancellationToken ct)
    {
        using var asUser = BackgroundUser.Use(userId);

        var set = await builder.BuildAsync(ct);
        if (set.Rows.Count == 0) throw new TrainingDataMissingException();

        var reportData = CategoryModelTrainer.Train(set.Rows);

        try
        {
            await store.Publish(reportData, userId, context?.BackgroundJob.Id, ct);
        }
        finally
        {
            await reportData.buffer.DisposeAsync();
        }

        return reportData.Item1;
    }
}
