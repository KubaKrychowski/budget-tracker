namespace BudgetTracker.Api.Features.Limits.Models;

/// <summary>Kategoria dopuszczona do limitu — klucz w bazie do zapytań i publiczny identyfikator do API.</summary>
public sealed record LimitCategory(int Id, Guid BusinessId, string Name);
