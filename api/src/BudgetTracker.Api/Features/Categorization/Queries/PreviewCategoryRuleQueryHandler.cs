using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categorization.Queries;

/// <summary>
/// Liczy, co dana reguła złapałaby na już zaimportowanych transakcjach. NIC NIE ZAPISUJE.
/// </summary>
/// <remarks>
/// <para>
/// Po co: walidator odrzuca tylko reguły beznadziejne (brak wzorca, niepoprawny regex, <c>min &gt;= max</c>),
/// a nie „wzorzec, którego nie ma w Twoich danych". Bez podglądu pisanie wyrażeń regularnych jest
/// zgadywaniem, a skutek pomyłki widać dopiero przy następnym imporcie.
/// </para>
/// <para>
/// ⚠️ Dopasowanie idzie przez <see cref="RuleMatcher"/> — ten sam kod, którego używa <see cref="RuleCategorizer"/>.
/// Druga implementacja warunków rozjechałaby się po pierwszej zmianie, a podgląd, który kłamie
/// o skutku reguły, jest gorszy niż jego brak: użytkownik zapisuje regułę właśnie w zaufaniu do niego.
/// </para>
/// <para>
/// ⚠️ Walidacja i sprawdzenie kategorii są TAKIE SAME jak przy zapisie (ten sam <see cref="CategoryRuleValidator"/>
/// i <see cref="CategoryRuleLookup"/>), żeby nie dało się zobaczyć ładnego podglądu reguły, której zapis
/// zaraz odrzuci 400 albo 404.
/// </para>
/// </remarks>
public sealed class PreviewCategoryRuleQueryHandler(
    AppDbContext db,
    CategoryRuleLookup lookup,
    TimeProvider clock)
{
    /// <summary>Ile miesięcy wstecz liczymy trafienia.</summary>
    /// <remarks>
    /// Okno, a nie cała historia: reguła ma odpowiadać na „co to złapie u mnie teraz", a skan rośnie
    /// liniowo z historią. Dwanaście miesięcy pokrywa sezonowość i zgadza się z tym, co pokazuje
    /// ekran oszczędności.
    /// </remarks>
    private const int MonthsBack = 12;

    /// <summary>Ile przykładowych trafień pokazujemy.</summary>
    /// <remarks>Lista ma przekonać, że wzorzec łapie to, co trzeba — nie zastąpić listy transakcji.</remarks>
    private const int SampleSize = 5;

    public async Task<RulePreviewResponseDto> HandleAsync(CategoryRuleRequestDto request, CancellationToken ct)
    {
        _ = await lookup.CategoryAsync(request.CategoryId, ct);
        CategoryRuleValidator.Validate(request);

        CompiledRule candidate;
        try
        {
            candidate = new CompiledRule(
                request.Priority,
                RuleMatcher.Compile(CategoryRuleValidator.Trim(request.Pattern)),
                RuleMatcher.Compile(CategoryRuleValidator.Trim(request.TransactionTypePattern)),
                request.Direction,
                CategoryId: 0,
                request.MinAmount,
                request.MaxAmount);
        }
        catch (ArgumentException)
        {
            // Walidator powinien to już złapać; jeśli nie, to i tak jest to błąd wejścia, nie awaria.
            throw new RulePatternInvalidException();
        }

        // ⚠️ Regułę traktujemy jak DOPISANĄ, więc przy równym priorytecie przegrywa z istniejącymi
        // (kolejność to priorytet, potem Id, a nowy wiersz dostaje najwyższe Id). Przy edycji jest to
        // założenie ostrożne: pokaże przesłonięć nie mniej, niż będzie w rzeczywistości.
        var existing = await RuleCategorizer.LoadRulesAsync(db, ct);
        var preceding = existing.Where(r => r.Priority <= request.Priority).ToList();

        var since = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date).AddMonths(-MonthsBack);

        var rows = await db.Transactions
            .Where(t => t.Date >= since)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Select(t => new
            {
                t.BusinessId, t.Date, t.Description, t.TransactionType, t.Amount, t.CategoryId,
            })
            .ToListAsync(ct);

        var categoryNames = await db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var matchCount = 0;
        var shadowedCount = 0;
        var samples = new List<RulePreviewMatchResponseDto>(SampleSize);

        foreach (var row in rows)
        {
            var normalized = DescriptionNormalizer.Normalize(row.Description);
            if (!RuleMatcher.Matches(candidate, normalized, row.TransactionType, row.Amount)) continue;

            matchCount++;

            var shadowed = preceding.Any(r => RuleMatcher.Matches(r, normalized, row.TransactionType, row.Amount));
            if (shadowed) shadowedCount++;

            if (samples.Count < SampleSize && !shadowed)
            {
                samples.Add(new RulePreviewMatchResponseDto(
                    row.BusinessId, row.Date, row.Description, row.TransactionType, row.Amount,
                    row.CategoryId is { } id && categoryNames.TryGetValue(id, out var name) ? name : null));
            }
        }

        // Gdy wszystkie trafienia są przesłonięte, przykłady i tak muszą być — inaczej ekran mówi
        // „12 trafień" i pokazuje pustą listę, co wygląda na usterkę, a nie na przesłonięcie.
        if (samples.Count == 0 && matchCount > 0)
        {
            foreach (var row in rows)
            {
                var normalized = DescriptionNormalizer.Normalize(row.Description);
                if (!RuleMatcher.Matches(candidate, normalized, row.TransactionType, row.Amount)) continue;

                samples.Add(new RulePreviewMatchResponseDto(
                    row.BusinessId, row.Date, row.Description, row.TransactionType, row.Amount,
                    row.CategoryId is { } id && categoryNames.TryGetValue(id, out var name) ? name : null));

                if (samples.Count == SampleSize) break;
            }
        }

        return new RulePreviewResponseDto(matchCount, shadowedCount, rows.Count, samples);
    }
}
