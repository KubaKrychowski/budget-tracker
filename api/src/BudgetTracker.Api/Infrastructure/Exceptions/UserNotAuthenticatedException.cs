namespace BudgetTracker.Api.Infrastructure.Exceptions;

/// <summary>
/// Żądanie nie niesie identyfikatora użytkownika (brak roszczenia <c>sub</c>). Warstwa HTTP tłumaczy to na 401.
/// </summary>
/// <remarks>
/// Rzucany przez <see cref="ICurrentUserAccessor.UserId"/>, więc handler, który potrzebuje właściciela,
/// czyta po prostu <c>currentUser.UserId</c> — bez własnego guarda na <c>null</c> w każdej komendzie.
/// Przy poprawnie skonfigurowanym uwierzytelnianiu to nieosiągalne (fallback policy odrzuca anonimowe
/// żądania wcześniej), więc jeśli poleci, to token bez <c>sub</c>, a nie normalny przypadek.
/// </remarks>
public sealed class UserNotAuthenticatedException()
    : Exception("Żądanie nie zawiera identyfikatora zalogowanego użytkownika (sub).");
