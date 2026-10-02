using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Categories.Contracts;

/// <summary>Kategoria na liście zarządzania razem z jej użyciem.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> kategorii.</param>
/// <param name="IsShared">Kategoria wspólna (bazowa) — tylko do odczytu.</param>
/// <param name="Transactions">Transakcje zalogowanego konta w tej kategorii.</param>
/// <param name="Rules">Reguły kategoryzacji (własne i wspólne) wskazujące na tę kategorię.</param>
/// <param name="Limits">Limity budżetowe konta na tej kategorii.</param>
/// <param name="InUse">Czy kategoria jest używana przez transakcje, reguły albo limity — wtedy nie da się zmienić jej typu.</param>
public sealed record CategoryResponseDto(
    Guid Id,
    string Name,
    CategoryType Type,
    bool IsShared,
    int Transactions,
    int Rules,
    int Limits,
    bool InUse);
