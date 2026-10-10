namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Kopia istniejącej strategii — z tym samym grafem i parametrami, pod nową nazwą.</summary>
/// <param name="Name">Nazwa kopii, 1–100 znaków.</param>
/// <param name="CopyVariants">Czy kopiować warianty; <c>false</c> zostawia tylko wariant bazowy.</param>
public sealed record DuplicateStrategyRequestDto(string Name, bool CopyVariants = true);
