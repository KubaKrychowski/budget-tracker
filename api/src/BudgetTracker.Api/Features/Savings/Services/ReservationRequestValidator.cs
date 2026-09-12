using BudgetTracker.Api.Features.Savings.Contracts;
using BudgetTracker.Api.Features.Savings.Exceptions;

namespace BudgetTracker.Api.Features.Savings.Services;

/// <summary>Walidacja założenia i edycji rezerwacji — jedna reguła dla obu zapisów.</summary>
public static class ReservationRequestValidator
{
    /// <summary>Przycięta nazwa i kwota; pusta nazwa albo niedodatnia kwota kończą się 400.</summary>
    public static (string Name, decimal Amount) Validate(SaveReservationRequestDto request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ReservationNameRequiredException();
        if (request.Amount <= 0) throw new ReservationAmountInvalidException();
        return (name, request.Amount);
    }
}
