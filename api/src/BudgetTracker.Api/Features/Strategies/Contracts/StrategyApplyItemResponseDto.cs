using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Strategies.Consts;

namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Jedna akcja „do budżetu” w oknie „Zastosuj w budżecie”.</summary>
/// <param name="NodeId">Identyfikator kafelka na tablicy.</param>
/// <param name="Type">Rodzaj akcji — klient składa z niego opis.</param>
/// <param name="Title">Podpis kafelka (nazwa rezerwacji, wydatku); pusty, gdy użytkownik go nie wpisał.</param>
/// <param name="Amount">Kwota akcji.</param>
/// <param name="Status">Co by się stało przy zastosowaniu.</param>
/// <param name="Month">
/// Miesiąc, od którego (limit) albo na który (wydatek jednorazowy) akcja zadziała, albo ostatni miesiąc zlecenia stałego;
/// dla rezerwacji — jej termin. Może być pusty.
/// </param>
/// <param name="CurrentAmount">Kwota, którą akcja zastąpi (obecny cel albo limit); <c>null</c>, gdy nie ma czego zastępować.</param>
public sealed record StrategyApplyItemResponseDto(
    string NodeId,
    StrategyNodeType Type,
    string Title,
    decimal? Amount,
    StrategyApplyStatus Status,
    DateOnly? Month,
    decimal? CurrentAmount);
