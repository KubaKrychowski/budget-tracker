namespace BudgetTracker.Api.Features.Admin.Contracts;

/// <summary>Bilet jednorazowy do wejścia do panelu zadań w tle (<c>/hangfire/enter?ticket=…</c>).</summary>
public sealed record JobsDashboardTicketResponseDto(string Ticket);
