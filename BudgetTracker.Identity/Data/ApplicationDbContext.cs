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
    /// <summary>Dziennik audytu usuwania kont i operacji na danych bez właściciela — tylko dopisywany.</summary>
    public DbSet<AdminAuditEntry> AdminAudit => Set<AdminAuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseOpenIddict();

        builder.Entity<AdminAuditEntry>(e =>
        {
            e.ToTable("AdminAuditLog");
            e.HasKey(x => x.Id);
            // Nazwa członka jako tekst: dziennik czyta się w SQL-u, a numery enumów mówiłyby tam niewiele.
            e.Property(x => x.Action).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.SubjectEmailHash).HasMaxLength(64);
            // Ekran „Historia" czyta od najnowszych; SubjectId służy do odnalezienia wpisów jednego konta.
            e.HasIndex(x => x.OccurredAt);
            e.HasIndex(x => x.SubjectId);
            e.HasIndex(x => x.SubjectEmailHash);
        });
    }
}
