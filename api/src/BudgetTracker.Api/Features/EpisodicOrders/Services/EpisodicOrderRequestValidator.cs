using BudgetTracker.Api.Features.EpisodicOrders.Contracts;
using BudgetTracker.Api.Features.EpisodicOrders.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.EpisodicOrders.Services;

/// <summary>Walidacja zapisu zlecenia epizodycznego — nazwa, opis i plan.</summary>
public sealed class EpisodicOrderRequestValidator(AppDbContext db)
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 1000;

    /// <summary>Nazwa po przycięciu i opis (pusty = <c>null</c>).</summary>
    /// <remarks>Za długą nazwę przycinamy, a nie odrzucamy: przy „Oznacz jako zlecenie epizodyczne” podpowiedzią jest
    /// tytuł z wyciągu, który bywa dłuższy niż kolumna.</remarks>
    public static (string Name, string? Description) Texts(SaveEpisodicOrderRequestDto request)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0) throw new EpisodicOrderNameRequiredException();

        var description = request.Description?.Trim();
        return (
            name.Length > MaxNameLength ? name[..MaxNameLength] : name,
            string.IsNullOrEmpty(description) ? null
            : description.Length > MaxDescriptionLength ? description[..MaxDescriptionLength] : description);
    }

    /// <summary>Plan zakupu: kategoria (wewnętrzny klucz), dodatnia kwota i opcjonalny miesiąc terminu.</summary>
    public async Task<(int CategoryId, decimal Amount, DateOnly? DueMonth)> PlanAsync(
        SaveEpisodicOrderRequestDto request, CancellationToken ct)
    {
        if (request.CategoryId is not { } categoryId || request.Amount is not { } amount || amount <= 0)
        {
            throw new EpisodicOrderPlanInvalidException();
        }

        var category = await db.Categories
            .Where(c => c.BusinessId == categoryId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);

        return category is { } id
            ? (id, amount, request.DueMonth is { } due ? new DateOnly(due.Year, due.Month, 1) : null)
            : throw new EpisodicOrderPlanInvalidException();
    }
}
