using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categorization.Commands;

/// <summary>Powrót do wcześniejszej wersji modelu. Nic nie kasuje — droga działa w obie strony.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>⚠️ <c>CancellationToken</c> jest parametrem METODY, nie konstruktora: kontener DI nie ma czego
/// wstrzyknąć pod ten typ, więc handler z tokenem w konstruktorze wywalał się przy rozwiązywaniu
/// endpointu — czyli w czasie wykonania, a nie kompilacji.</item>
/// <item>Nieznana wersja to <c>404</c> (<see cref="ModelVersionNotFoundException"/>), nie awaria.
/// Wersje są zawężone filtrem własnościowym, więc cudza wersja wygląda tak samo jak nieistniejąca.</item>
/// <item>Brak aktywnej wersji NIE jest błędem: przed pierwszym treningiem nie ma czego wyłączać.</item>
/// </list>
/// </remarks>
public sealed class ActivateCategoryModelCommandHandler(AppDbContext db)
{
    public async Task HandleAsync(Guid versionId, CancellationToken ct)
    {
        var versions = await db.Set<ModelVersion>().ToListAsync(ct);

        var newActiveVersion = versions.FirstOrDefault(v => v.BusinessId == versionId)
            ?? throw new ModelVersionNotFoundException(versionId.ToString());

        versions.FirstOrDefault(v => v.Active)?.Deactivate();
        newActiveVersion.Activate();

        await db.SaveChangesAsync(ct);
    }
}
