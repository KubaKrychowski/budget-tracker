namespace BudgetTracker.Api.Features.Receipts.Contracts;

/// <summary>Transakcja z importu, która może być tą z paragonu.</summary>
/// <param name="Id">Identyfikator transakcji.</param>
/// <param name="Amount">Kwota ze znakiem, jak w wyciągu (wydatek ujemny).</param>
/// <param name="Match"><c>Exact</c> — ta sama kwota i data w oknie dni; <c>Possible</c> — kwota zbliżona.</param>
public sealed record ReceiptCandidateResponseDto(Guid Id, DateOnly Date, string Description, decimal Amount, string Match);
