using System.ComponentModel.DataAnnotations;
using BudgetTracker.Identity.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BudgetTracker.Identity.Controllers;

/// <summary>
/// Treść żądania z formularza na landingu.
/// </summary>
/// <param name="Email">Adres, na który ma przyjść zaproszenie.</param>
/// <param name="Consent">Zgoda na przetwarzanie adresu, Regulamin i Polityka prywatności — musi być <c>true</c>.</param>
/// <param name="Website">
/// Pułapka na boty: pole ukryte na stronie, którego człowiek nie wypełnia. Niepuste = automat.
/// </param>
public sealed record BetaRequestDto(string? Email, bool Consent, string? Website);

/// <summary>
/// Publiczny endpoint prośby o dostęp do bety — jedyne anonimowe wejście do tego serwisu poza logowaniem.
/// </summary>
/// <remarks>
/// <para>
/// Bez ciasteczek i bez tokenu: żądanie idzie z innego originu (landing), więc ochronę przed nadużyciem dają CORS
/// zawężony do originu strony, limit tempa na IP i pole-pułapka, a nie antiforgery.
/// </para>
/// <para>
/// ⚠️ Odpowiedź <c>202</c> jest taka sama dla nowego i dla powtórzonego adresu oraz dla wypełnionej pułapki —
/// wołający nie ma się dowiedzieć, czy adres już był na liście ani że go wzięliśmy za automat.
/// </para>
/// </remarks>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[EnableCors(CorsPolicies.Landing)]
public sealed class BetaRequestsController(BetaAccessRequestService requests, ILogger<BetaRequestsController> logger)
    : ControllerBase
{
    [HttpPost("/api/beta-requests")]
    [EnableRateLimiting("beta-request")]
    public async Task<IActionResult> Submit([FromBody] BetaRequestDto? dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest(new { error = "invalid_email" });

        // Pułapka pierwsza i po cichu: automatowi nie mówimy, że go rozpoznaliśmy, i niczego nie zapisujemy.
        if (!string.IsNullOrWhiteSpace(dto.Website)) return Accepted();

        var email = dto.Email?.Trim();
        if (string.IsNullOrEmpty(email) || email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
        {
            return BadRequest(new { error = "invalid_email" });
        }

        if (!dto.Consent) return BadRequest(new { error = "consent_required" });

        var outcome = await requests.SubmitAsync(email, ct);
        // Adresu nie logujemy: dziennik nie jest miejscem na dane osobowe osób, które nie mają konta.
        logger.LogInformation("Prośba o dostęp do bety: {Outcome}.", outcome);

        return Accepted();
    }
}

/// <summary>Nazwy polityk CORS — jedno miejsce, żeby literówka w atrybucie nie wyłączyła polityki po cichu.</summary>
public static class CorsPolicies
{
    public const string Spa = "spa-client";

    public const string Landing = "landing";
}
