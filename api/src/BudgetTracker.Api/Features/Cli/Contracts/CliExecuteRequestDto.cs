namespace BudgetTracker.Api.Features.Cli.Contracts;

/// <summary>Ciało <c>POST /api/cli/execute</c> — cała linia komendy jako jeden string, jak w prawdziwym terminalu.</summary>
public sealed record CliExecuteRequestDto(string Line);
