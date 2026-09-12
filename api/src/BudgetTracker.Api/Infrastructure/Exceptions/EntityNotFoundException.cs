namespace BudgetTracker.Api.Infrastructure.Exceptions;

/// <summary>
/// Wskazany <c>BusinessId</c> nie istnieje. Warstwa HTTP tłumaczy to na 404.
/// </summary>
/// <remarks>
/// Osobny typ, a nie zwykły <see cref="InvalidOperationException"/>, bo to NIE jest awaria:
/// to normalna odpowiedź na złe wejście. Cichy fallback na „pierwszy z brzegu" byłby gorszy —
/// użytkownik zobaczyłby cudze dane, nie wiedząc, że pomylił adres.
/// </remarks>
public class EntityNotFoundException(string entity, Guid businessId)
    : Exception($"{entity} o BusinessId {businessId} nie istnieje.")
{
    public string Entity { get; } = entity;
    public Guid BusinessId { get; } = businessId;
}
