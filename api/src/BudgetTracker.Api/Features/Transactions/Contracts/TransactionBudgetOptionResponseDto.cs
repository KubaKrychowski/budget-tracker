namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>
/// Budżet jako pozycja multiselecta nad listą.
/// </summary>
/// <remarks>
/// ⚠️ Kształtem to bliźniak <c>Dashboard.Contracts.BudgetOptionResponseDto</c> i jest to ŚWIADOME powtórzenie,
/// nie przeoczenie. Slice ma własny kontrakt (CLAUDE.md §4) — sięgnięcie po typ sąsiedniego
/// feature'u związałoby dwa ekrany tak, że zmiana pod dashboard psułaby listę transakcji.
/// Front i tak widzi identyczny JSON i używa jednego modelu.
///
/// <c>Disabled</c> jest wystawiony, bo wyłączonych budżetów NIE ukrywamy (CLAUDE.md §5):
/// historię ogląda się także po zamknięciu budżetu, więc trafiają na listę z tagiem.
/// </remarks>
public sealed record TransactionBudgetOptionResponseDto(Guid Id, string Name, DateOnly Month, bool Disabled);
