using BudgetTracker.Api.Features.Budgets.Services;
using Hangfire;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>
/// Sprząta WSZYSTKIE budżety oznaczone jako usunięte, nie czekając na <see cref="BudgetOptions.RetentionDays"/>.
/// Zadanie Hangfire <c>purge-deleted-budgets-now</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Nie ma harmonogramu i nie może go dostać</b> — jest zarejestrowane z <c>Cron.Never()</c>, żeby
/// stanąć w panelu <c>/hangfire</c> z przyciskiem „Trigger now". Retencja jest OBIETNICĄ SKŁADANĄ
/// UŻYTKOWNIKOWI: modal usuwania mówi „odwracalne przez N dni" i bierze N z ustawień. Cykliczny przebieg
/// tego zadania sprowadziłby to okno do zera i zamieniłby tamten komunikat w kłamstwo — przy PIERWSZYM
/// przebiegu, po cichu. Dlatego wyzwala je człowiek, świadomie, i to jest cała różnica wobec
/// <see cref="PurgeDeletedBudgetsCommandHandler"/>.
/// </para>
/// <para>
/// Powód, dla którego mimo wszystko istnieje: „usuń teraz" jest potrzebne, gdy w budżecie wylądowały dane,
/// których nie wolno trzymać ani dnia dłużej (import na zły budżet, cudzy wyciąg, pomyłka przed publikacją).
/// Czekanie na okno retencji jest wtedy dokładnie odwrotnością tego, po co ono jest.
/// </para>
/// <para>
/// Kasuje wyłącznie to, co JUŻ jest oznaczone do usunięcia — nie omija kroku „usuń budżet", tylko wyłącznie
/// czekanie. Żywego budżetu nie tknie, o co dba <see cref="BudgetPurger"/>.
/// </para>
/// </remarks>
public sealed class ForcePurgeDeletedBudgetsCommandHandler(
    BudgetPurger purger,
    ILogger<ForcePurgeDeletedBudgetsCommandHandler> logger)
{
    /// <returns>Liczba usuniętych budżetów — Hangfire zapisuje ją jako wynik przebiegu w historii.</returns>
    /// <remarks>
    /// Loguje na poziomie <c>Warning</c>, nie <c>Information</c>. To nie jest awaria, ale JEST pominięciem
    /// obietnicy danej użytkownikowi — a historia przebiegów jest tu audytem (CLAUDE.md §4), więc ślad po
    /// takim przebiegu ma się dać znaleźć bez przekopywania logów informacyjnych.
    /// </remarks>
    [AutomaticRetry(Attempts = 3)]
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var purged = await purger.PurgeAsync(deletedBefore: null, ct);

        logger.LogWarning(
            "Wymuszone sprzątanie: usunięto trwale {Count} budżetów z pominięciem okna retencji.", purged);

        return purged;
    }
}
