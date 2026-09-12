using BudgetTracker.Api.Features.Categorization;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Features.Import.Services;

/// <summary>Próg pewności importu — z konfiguracji kategoryzacji.</summary>
public sealed class ImportConfidenceThreshold(IOptions<CategorizationOptions> options)
{
    private readonly decimal _threshold = options.Value.ConfidenceThreshold;

    /// <summary>
    /// Czy pewność wystarcza, żeby kategoria weszła bez przeglądu.
    /// </summary>
    /// <remarks>
    /// Jedno miejsce na tę regułę w tym slice'ie: pyta o nią i podgląd (co pokazać jako
    /// „do weryfikacji"), i zapis (jaki status utrwalić). Rozjazd między nimi znaczyłby,
    /// że ekran obiecuje co innego, niż baza zapamiętuje.
    /// </remarks>
    public bool IsConfident(decimal? confidence) => (confidence ?? 0m) >= _threshold;
}
