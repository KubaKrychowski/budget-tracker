namespace BudgetTracker.Api.Features.Admin.Contracts;

/// <summary>Konto, które przejmuje dane. Musi być istniejącym kontem — pilnuje tego serwer tożsamości, API nie zna kont.</summary>
public sealed record ReassignOwnerRequestDto(Guid TargetUserId);
