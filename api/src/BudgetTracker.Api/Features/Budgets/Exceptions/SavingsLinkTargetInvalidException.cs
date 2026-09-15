namespace BudgetTracker.Api.Features.Budgets.Exceptions;

/// <summary>
/// Wskazany budżet oszczędnościowy nie istnieje albo jest tym samym budżetem, co ten, który go wskazuje.
/// </summary>
/// <remarks>
/// 400, nie 404: identyfikator przychodzi w CIELE żądania, nie w adresie zasobu — ta sama zasada co
/// przy imporcie i rozliczeniu rezerwacji.
/// </remarks>
public sealed class SavingsLinkTargetInvalidException : Exception;
