namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Jedna transakcja, którą reguła by złapała — tyle, ile trzeba, żeby ją rozpoznać na ekranie.</summary>
/// <remarks>
/// Opis jedzie w postaci SUROWEJ, nie znormalizowanej, mimo że dopasowanie działa na znormalizowanej.
/// Użytkownik ma rozpoznać swoją transakcję, a nie zobaczyć, co z niej zrobił normalizator.
/// </remarks>
public sealed record RulePreviewMatchResponseDto(
    Guid Id,
    DateOnly Date,
    string Description,
    string TransactionType,
    decimal Amount,
    string? CurrentCategoryName);
