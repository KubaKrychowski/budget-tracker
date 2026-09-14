namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>
/// Budżet jako pozycja przełącznika nad ekranem oszczędności i listą rezerwacji.
/// </summary>
/// <remarks>
/// ⚠️ Kształtem to bliźniak <c>Transactions.Contracts.TransactionBudgetOptionResponseDto</c> i jest to ŚWIADOME
/// powtórzenie — slice ma własny kontrakt (CLAUDE.md §4). Front widzi identyczny JSON i używa jednego modelu.
///
/// <c>Disabled</c> jest wystawiony, bo wyłączonych budżetów NIE ukrywamy (CLAUDE.md §5) — trafiają na listę z tagiem.
/// </remarks>
public sealed record SavingsBudgetOptionResponseDto(Guid Id, string Name, DateOnly Month, bool Disabled);
