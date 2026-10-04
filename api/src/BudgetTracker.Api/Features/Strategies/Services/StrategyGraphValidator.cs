using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Strategies.Contracts;
using BudgetTracker.Api.Features.Strategies.Exceptions;
using BudgetTracker.Api.Features.Strategies.Models;

namespace BudgetTracker.Api.Features.Strategies.Services;

/// <summary>Strukturalna walidacja i normalizacja żądania zapisu strategii.</summary>
/// <remarks>
/// Tu są tylko błędy, których nie da się zapisać (powtórzony identyfikator, połączenie do nieistniejącego węzła).
/// Brak parametru albo osierocony węzeł to PROBLEM grafu (<see cref="StrategyGraphAnalyzer"/>), a nie błąd — szkic
/// zapisuje się zawsze.
/// </remarks>
public static class StrategyGraphValidator
{
    /// <summary>Górne granice rozmiaru — chronią kolumnę <c>jsonb</c> i symulator przed absurdalnym wejściem.</summary>
    public const int MaxNodes = 200;

    public const int MaxEdges = 400;

    private const int MaxIdLength = 64;
    private const int MaxTitleLength = 100;
    public const int MaxNameLength = 100;
    private const decimal MaxAmount = 1_000_000_000_000m;
    private const double MaxCoordinate = 100_000d;

    public static ValidStrategy Validate(SaveStrategyRequestDto request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength) throw new StrategyNameRequiredException();
        if (request.HorizonMonths is < Strategy.MinHorizonMonths or > Strategy.MaxHorizonMonths)
        {
            throw new StrategyHorizonInvalidException();
        }

        if (Math.Abs(request.StartCash) > MaxAmount) throw new StrategyGraphInvalidException();

        var nodes = request.Nodes ?? [];
        var edges = request.Edges ?? [];
        if (nodes.Count > MaxNodes || edges.Count > MaxEdges) throw new StrategyGraphInvalidException();

        var domainNodes = new List<StrategyNode>(nodes.Count);
        var ids = new HashSet<string>();
        foreach (var n in nodes)
        {
            var id = n.Id?.Trim();
            if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength || !ids.Add(id)) throw new StrategyGraphInvalidException();
            if (!Enum.IsDefined(n.Type)) throw new StrategyGraphInvalidException();
            if (!IsFinite(n.X) || !IsFinite(n.Y)) throw new StrategyGraphInvalidException();
            foreach (var amount in new[] { n.Amount, n.Rate, n.Installment, n.Threshold })
            {
                if (amount is { } value && Math.Abs(value) > MaxAmount) throw new StrategyGraphInvalidException();
            }

            if (n.Mode is { } mode && !Enum.IsDefined(mode)) throw new StrategyGraphInvalidException();
            if (n.Metric is { } metric && !Enum.IsDefined(metric)) throw new StrategyGraphInvalidException();
            if (n.Comparison is { } comparison && !Enum.IsDefined(comparison)) throw new StrategyGraphInvalidException();

            var title = (n.Title ?? string.Empty).Trim();
            if (title.Length > MaxTitleLength) throw new StrategyGraphInvalidException();
            domainNodes.Add(new StrategyNode(
                id, n.Type, title, n.X, n.Y, n.Month is { } m ? new DateOnly(m.Year, m.Month, 1) : null,
                n.Amount, n.Rate, n.Installment, n.Mode, n.Metric, n.Comparison, n.Threshold, n.CategoryId, n.StandingOrderId));
        }

        var types = domainNodes.ToDictionary(n => n.Id, n => n.Type);
        var domainEdges = new List<StrategyEdge>(edges.Count);
        var edgeIds = new HashSet<string>();
        foreach (var e in edges)
        {
            var id = e.Id?.Trim();
            var from = e.From?.Trim();
            var to = e.To?.Trim();
            if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength || !edgeIds.Add(id)) throw new StrategyGraphInvalidException();
            if (from is null || to is null || from == to) throw new StrategyGraphInvalidException();
            if (!types.TryGetValue(from, out var fromType) || !types.ContainsKey(to)) throw new StrategyGraphInvalidException();
            if (!Enum.IsDefined(e.Label)) throw new StrategyGraphInvalidException();
            if (e.Label != StrategyEdgeLabel.None && fromType != StrategyNodeType.Condition)
            {
                throw new StrategyGraphInvalidException();
            }

            domainEdges.Add(new StrategyEdge(id, from, to, e.Label));
        }

        return new ValidStrategy(
            name, new DateOnly(request.StartMonth.Year, request.StartMonth.Month, 1), request.StartCash,
            request.HorizonMonths, domainNodes, domainEdges);
    }

    private static bool IsFinite(double value) => double.IsFinite(value) && Math.Abs(value) <= MaxCoordinate;
}
