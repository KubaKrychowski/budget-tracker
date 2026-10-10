using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Features.Strategies.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Strategies.Commands;

/// <summary>Kopiuje strategię w obrębie tego samego budżetu i zwraca kopię z policzonym wynikiem.</summary>
/// <remarks>
/// <para>
/// Kopia jest NIEZALEŻNA od oryginału: ma własne listy węzłów, połączeń i wariantów, więc zmiana jednej nie rusza drugiej.
/// Identyfikatory kafelków zostają te same — to adresy WEWNĄTRZ strategii, nie globalne klucze.
/// </para>
/// <para>
/// ⚠️ Kopia nie jest „zastosowana w budżecie”: nic z tego, co oryginał założył w celach, rezerwacjach czy limitach, nie jest
/// kopiowane ani powielane. Odwołania kafelków do kategorii i zleceń stałych zostają, bo wskazują obiekty budżetu.
/// </para>
/// </remarks>
public sealed class DuplicateStrategyCommandHandler(AppDbContext db, StrategiesBudgetScope scope, ICurrentUserAccessor currentUser)
{
    public async Task<StrategyResponseDto> HandleAsync(Guid id, DuplicateStrategyRequestDto request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > StrategyGraphValidator.MaxNameLength)
        {
            throw new StrategyNameRequiredException();
        }

        var source = await db.Strategies.SingleOrDefaultAsync(s => s.BusinessId == id, ct)
            ?? throw new StrategyNotFoundException(id);

        var now = scope.Now();
        var copy = new Strategy(
            source.BudgetBusinessId, name, source.StartMonth, source.StartCash, source.HorizonMonths, now, currentUser.UserId);
        copy.Replace(
            name, source.StartMonth, source.StartCash, source.HorizonMonths, source.Nodes, source.Edges,
            request.CopyVariants
                ? [.. source.Variants.Select(v => new StrategyVariant(v.Id, v.Name, [.. v.DisabledNodeIds]))]
                : [],
            now);

        db.Strategies.Add(copy);
        await db.SaveChangesAsync(ct);

        return StrategyMapping.ToResponse(copy, StrategySimulator.Run(StrategyMapping.ToInput(copy)));
    }
}
