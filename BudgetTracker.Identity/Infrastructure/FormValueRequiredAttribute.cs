using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BudgetTracker.Identity.Infrastructure;

/// <summary>
/// Rozstrzyga, która z akcji obsłuży POST na <c>~/connect/authorize</c> — GET i POST (zgoda/odrzucenie)
/// muszą dzielić dokładnie ten sam adres, bo middleware OpenIddict rozpoznaje żądanie tylko na
/// skonfigurowanych URI endpointów (podścieżka typu <c>/accept</c> nie zostałaby zparsowana).
/// Wzorzec z oficjalnych przykładów OpenIddict (Zirku/Mvc.Server).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FormValueRequiredAttribute(string name) : Attribute, IActionConstraint
{
    public int Order => 0;

    public bool Accept(ActionConstraintContext context)
    {
        var request = context.RouteContext.HttpContext.Request;
        if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrEmpty(request.ContentType) ||
            !request.ContentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrEmpty(request.Form[name]);
    }
}
