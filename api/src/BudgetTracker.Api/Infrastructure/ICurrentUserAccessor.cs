using System.Security.Claims;
using BudgetTracker.Api.Infrastructure.Exceptions;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Zalogowany użytkownik odczytany z tokenu JWT wystawionego przez BudgetTracker.Identity.
/// Abstrakcja nad <see cref="HttpContext"/>, żeby <see cref="AppDbContext"/> nie zależał wprost
/// od pipeline'u HTTP (seedy i zadania Hangfire konstruują go poza żądaniem).
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// Roszczenie <c>sub</c> tokenu. Handler, który potrzebuje właściciela, czyta to wprost i nie robi
    /// własnego guarda.
    /// </summary>
    /// <exception cref="UserNotAuthenticatedException">Brak <c>sub</c> — warstwa HTTP tłumaczy na 401.</exception>
    Guid UserId { get; }

    /// <summary>
    /// To samo co <see cref="UserId"/>, ale <c>null</c> zamiast wyjątku. Dla infrastruktury, która
    /// działa także bez żądania HTTP (filtr „Owner" w <see cref="AppDbContext"/>, seedy, Hangfire) —
    /// tam brak użytkownika jest normalnym stanem, nie błędem uwierzytelnienia.
    /// </summary>
    Guid? UserIdOrNull { get; }
}

/// <remarks>
/// OpenIddict.Validation zostawia roszczenie pod krótką nazwą <c>sub</c> — sprawdzamy też
/// <see cref="ClaimTypes.NameIdentifier"/> na wypadek innego mapowania inbound (np. zwykły
/// <c>JwtBearer</c> z domyślną tabelą mapowań .NET).
/// </remarks>
public sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public Guid UserId => UserIdOrNull ?? throw new UserNotAuthenticatedException();

    public Guid? UserIdOrNull
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var sub = user?.FindFirst(SubjectClaimType)?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(sub, out var id)) return id;

            // Poza żądaniem (Hangfire, CLI) właściciel pochodzi z jawnego zakresu — patrz BackgroundUser.
            return BackgroundUser.UserId;
        }
    }

    private const string SubjectClaimType = "sub";
}
