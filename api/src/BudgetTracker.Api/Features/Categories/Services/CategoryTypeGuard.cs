using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categories.Exceptions;

namespace BudgetTracker.Api.Features.Categories.Services;

/// <summary>Reguła „typ kategorii a kierunek transakcji” przy ręcznym przypisaniu.</summary>
public static class CategoryTypeGuard
{
    /// <summary>Kategoria przychodowa przyjmuje tylko wpływy; wydatkowa przyjmuje wszystko (zwrot zakupu jest dodatni).</summary>
    public static bool Accepts(CategoryType type, decimal amount) => type != CategoryType.Income || amount >= 0;

    /// <summary>Rzuca <see cref="CategoryTypeMismatchException"/>, gdy kategoria nie przyjmuje takiej kwoty.</summary>
    public static void EnsureAccepts(CategoryType type, decimal amount)
    {
        if (!Accepts(type, amount)) throw new CategoryTypeMismatchException();
    }
}
