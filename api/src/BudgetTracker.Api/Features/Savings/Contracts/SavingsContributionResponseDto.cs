using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Jedna wpłata na rezerwację.</summary>
/// <param name="CategoryId">Kategoria limitu wpłaty ze zwykłego konta; <c>null</c> dla oszczędności.</param>
public sealed record SavingsContributionResponseDto(
    Guid Id, DateOnly Date, decimal Amount, ContributionSource Source, Guid? CategoryId);
