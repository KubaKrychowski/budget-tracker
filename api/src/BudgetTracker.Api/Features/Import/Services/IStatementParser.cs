using BudgetTracker.Api.Features.Import.Contracts;
using BudgetTracker.Api.Features.Import.Exceptions;
using BudgetTracker.Api.Features.Import.Models;

namespace BudgetTracker.Api.Features.Import.Services;

/// <summary>
/// Drugi twardy szew projektu (CLAUDE.md §4): parser per bank.
/// </summary>
/// <remarks>
/// Nowy bank = nowa implementacja + rejestracja w <c>ImportModule</c>, BEZ dotykania handlerów importu.
/// To jest kryterium akceptacji z issue, nie tylko preferencja.
/// </remarks>
public interface IStatementParser
{
    /// <summary>Identyfikator banku, po którym endpoint wybiera parser (np. „pko").</summary>
    string BankKey { get; }

    /// <summary>
    /// Rzuca <see cref="StatementFormatException"/>, gdy plik nie ma oczekiwanego formatu.
    /// Pojedynczy uszkodzony wiersz nie może przerwać całego importu — parser go pomija.
    /// </summary>
    Task<IReadOnlyList<ParsedRow>> ParseAsync(Stream content, CancellationToken ct);
}
