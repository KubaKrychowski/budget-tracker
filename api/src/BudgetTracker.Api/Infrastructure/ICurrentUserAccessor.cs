using System.Security.Claims;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Zalogowany użytkownik odczytany z tokenu JWT wystawionego przez BudgetTracker.Identity.
/// Abstrakcja nad <see cref="HttpContext"/>, żeby <see cref="AppDbContext"/> nie zależał wprost
/// od pipeline'u HTTP (seedy i zadania Hangfire konstruują go poza żądaniem).
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>Roszczenie <c>sub</c> tokenu; <c>null</c> poza kontekstem żądania HTTP.</summary>
    Guid? UserId { get; }
}

/// <remarks>
/// OpenIddict.Validation zostawia roszczenie pod krótką nazwą <c>sub</c> — sprawdzamy też
/// <see cref="ClaimTypes.NameIdentifier"/> na wypadek innego mapowania inbound (np. zwykły
/// <c>JwtBearer</c> z domyślną tabelą mapowań .NET).
/// </remarks>
public sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public Guid? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var sub = user?.FindFirst("sub")?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }
}
