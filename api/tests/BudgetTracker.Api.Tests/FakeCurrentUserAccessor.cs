using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Tests;

/// <summary>Zalogowany użytkownik na potrzeby testów handlerów tworzonych wprost (bez DI/HTTP).</summary>
internal sealed class FakeCurrentUserAccessor(Guid userId) : ICurrentUserAccessor
{
    public Guid? UserId { get; } = userId;
}
