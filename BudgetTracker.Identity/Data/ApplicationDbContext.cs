using BudgetTracker.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Identity.Data;

/// <summary>
/// Baza serwera tożsamości: konta ASP.NET Identity (klucze <see cref="Guid"/>, zgodne z <c>sub</c> w tokenie)
/// plus encje OpenIddict (klienci, zakresy, autoryzacje, tokeny) dopisane przez <c>UseOpenIddict()</c> niżej.
/// Osobna baza od <c>budgettracker</c> API — serwer tożsamości to osobny serwis.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseOpenIddict();
    }
}
