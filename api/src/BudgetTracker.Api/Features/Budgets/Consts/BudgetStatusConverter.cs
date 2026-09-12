using System.Text.Json;
using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Features.Budgets.Consts;

/// <summary>Enum jako tekst w camelCase — tak samo jak nazwy pól.</summary>
public sealed class BudgetStatusConverter()
    : JsonStringEnumConverter<BudgetStatus>(JsonNamingPolicy.CamelCase);
