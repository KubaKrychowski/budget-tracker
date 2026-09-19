using System.Security.Cryptography;
using System.Text;

namespace BudgetTracker.Identity.Services.Audit;

/// <summary>Skrót adresu e-mail do dziennika audytu: pozwala odnaleźć wpis po znanym adresie, nie ujawniając go.</summary>
public static class AuditEmailHash
{
    /// <summary>SHA-256 adresu po przycięciu i zamianie na małe litery (adres nie rozróżnia wielkości), zapis szesnastkowy.</summary>
    public static string? Compute(string? email) =>
        string.IsNullOrWhiteSpace(email)
            ? null
            : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));
}
