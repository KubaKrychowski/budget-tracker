using BudgetTracker.Api.Features.Import.Models;

namespace BudgetTracker.Api.Features.Import.Contracts;

/// <summary>
/// Wiersz odesłany przez front przy zatwierdzeniu — już PO usunięciach i korektach
/// użytkownika z kroku 3.
/// </summary>
/// <param name="Edited">
/// Czy kategorię wskazał człowiek. Rozstrzyga status: korekta użytkownika nie może
/// wrócić do kolejki „do przeglądu", którą właśnie ręcznie rozbroił.
/// </param>
public sealed record CommitRowRequestDto(
    DateOnly Date,
    decimal Amount,
    string Description,
    string TransactionType,
    string? ExternalReference,
    Guid? CategoryId,
    decimal? Confidence,
    bool Edited)
{
    /// <summary>Do wyliczenia klucza deduplikacji — ta sama logika co przy parsowaniu.</summary>
    public ParsedRow ToParsedRow() =>
        new(Date, Amount, Description, TransactionType, ExternalReference);
}
