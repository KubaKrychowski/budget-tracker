using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>„Wpłać na cel” — kwota umownie odkładana na rezerwację z oszczędności albo ze zwykłego konta.</summary>
/// <param name="Source">Konto, z którego odkładasz. Domyślnie oszczędnościowe — jak wpłaty sprzed wyboru konta.</param>
/// <param name="CategoryId">
/// Kategoria, do której limitu wlicza się wpłata. Wymagana dla <see cref="ContributionSource.Regular"/>,
/// dla oszczędności ignorowana.
/// </param>
public sealed record ContributeRequestDto(
    decimal Amount, ContributionSource Source = ContributionSource.Savings, Guid? CategoryId = null);
