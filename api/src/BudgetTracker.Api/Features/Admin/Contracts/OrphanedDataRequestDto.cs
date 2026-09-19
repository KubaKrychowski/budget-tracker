namespace BudgetTracker.Api.Features.Admin.Contracts;

/// <summary>Wszystkie konta, które istnieją w serwerze tożsamości — dane należące do KOGOKOLWIEK innego są bez właściciela.</summary>
public sealed record OrphanedDataRequestDto(IReadOnlyList<Guid> KnownUserIds);
