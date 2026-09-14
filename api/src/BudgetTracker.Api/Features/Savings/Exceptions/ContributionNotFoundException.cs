using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Features.Savings.Exceptions;

/// <summary>Wpłata o wskazanym identyfikatorze nie istnieje w tej rezerwacji.</summary>
public sealed class ContributionNotFoundException(Guid contributionId) : EntityNotFoundException("SavingsContribution", contributionId);
