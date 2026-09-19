namespace BudgetTracker.Identity.Services.Api;

/// <summary>API budżetu nie odpowiedziało poprawnie (brak połączenia, timeout albo kod błędu).</summary>
/// <remarks>
/// Osobny wyjątek, żeby wywołujący odróżnił „nie da się teraz" od błędu programisty — usuwanie konta przy takiej
/// awarii ma się zatrzymać PRZED skasowaniem konta, żeby nie zostawić danych bez właściciela.
/// </remarks>
public sealed class UserDataServiceException(string message, Exception? inner = null) : Exception(message, inner);
