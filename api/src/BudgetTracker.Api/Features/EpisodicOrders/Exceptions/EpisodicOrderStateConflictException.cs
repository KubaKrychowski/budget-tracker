namespace BudgetTracker.Api.Features.EpisodicOrders.Exceptions;

/// <summary>
/// Operacja nie pasuje do stanu zlecenia: cel oszczędzania dla zrealizowanego albo drugi raz, „kupione” dla już
/// zrealizowanego, „cofnij” dla zlecenia, które nigdy nie było zaplanowane.
/// </summary>
/// <remarks>409, bo żądanie jest poprawne — to stan zasobu na nie nie pozwala.</remarks>
public sealed class EpisodicOrderStateConflictException : Exception;
