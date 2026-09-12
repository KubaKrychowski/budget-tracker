using BudgetTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Bezpiecznik na fizyczne kasowanie: zamienia każdy <see cref="EntityState.Deleted"/> na
/// <see cref="EntityState.Modified"/> ze stemplem <see cref="Entity.DeletedAt"/>.
///
/// <para>
/// To jedyna rzecz, której nie da się załatwić w encji — <c>BusinessId</c> nadaje sobie sama
/// (patrz <see cref="Entity.BusinessId"/>), ale konstruktor nie ma jak przechwycić
/// <c>db.Remove(x)</c>.
/// </para>
///
/// <para>
/// ⚠️ To BEZPIECZNIK, nie mechanizm kasowania. Łapie pojedynczą encję i nic nie wie o jej
/// dzieciach — usunięcie budżetu razem z transakcjami, importami i limitami musi zostać
/// napisane jawnie w handlerze. „Kaskada" w tym projekcie jest w kodzie, nie w bazie.
/// </para>
/// </summary>
public sealed class SoftDeleteInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Intercept(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Intercept(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Intercept(DbContext? context)
    {
        if (context is null) return;

        var now = clock.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            if (entry.State != EntityState.Deleted) continue;

            entry.State = EntityState.Modified;
            entry.Entity.MarkDeleted(now);

            // Bez tego EF przy stanie Modified wysłałby UPDATE wszystkich kolumn.
            // Interesuje nas jedna — reszta ma zostać nietknięta.
            MarkOnlyDeletedAtAsModified(entry);
        }
    }

    private static void MarkOnlyDeletedAtAsModified(EntityEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            property.IsModified = property.Metadata.Name == nameof(Entity.DeletedAt);
        }
    }
}
