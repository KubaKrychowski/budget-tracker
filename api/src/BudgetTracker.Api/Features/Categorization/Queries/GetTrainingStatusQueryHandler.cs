using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categorization.Queries;

/// <summary>
/// Wynik treningu zgłoszonego przez <c>POST /api/categorization/train</c> — do odpytywania z ekranu.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Pytamy WYŁĄCZNIE bazę: zadanie odkłada wynik jako wiersz <see cref="ModelVersion"/>, więc
/// obecność wiersza z tym <c>JobId</c> JEST odpowiedzią „gotowe”. Czytanie stanu z Hangfire wiązałoby
/// kontrakt HTTP z jego wewnętrznym formatem i jego retencją historii.</item>
/// <item>⚠️ Brak wiersza znaczy „jeszcze nie ma wyniku” i obejmuje TRZY sytuacje: zadanie czeka w kolejce,
/// trwa albo się nie powiodło. Ekran ma o tym mówić ostrożnie („trening w toku”), a nieudane przebiegi
/// widać w panelu Hangfire — tam jest ich audyt.</item>
/// <item>Cudzego zgłoszenia nie da się podejrzeć: wersje zawęża filtr własnościowy i polityki RLS,
/// więc cudze <c>JobId</c> zwraca dokładnie to samo, co zmyślone.</item>
/// </list>
/// </remarks>
public sealed class GetTrainingStatusQueryHandler(AppDbContext db)
{
    public async Task<TrainingStatusResponseDto> HandleAsync(string jobId, CancellationToken ct)
    {
        var version = await db.Set<ModelVersion>()
            .FirstOrDefaultAsync(v => v.JobId == jobId, ct);

        if (version is null) return TrainingStatusResponseDto.Pending;

        return new TrainingStatusResponseDto(
            true,
            new TrainingReportResponseDto(
                version.TrainingSet,
                version.CategoriesCount,
                (double)version.Accuracy,
                (double)version.AverageAccuracy));
    }
}
