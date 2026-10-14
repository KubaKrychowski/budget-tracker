using BudgetTracker.Api.Features.Dashboard.Consts;

namespace BudgetTracker.Api.Features.Dashboard.Contracts;

/// <summary>Jeden wiersz rozbicia wydatków kategorii na produkty z paragonów, jako wartość dodatnia.</summary>
/// <param name="ProductCategoryId"><c>BusinessId</c> kategorii produktu; <c>null</c> dla wierszy innych niż <see cref="ProductSpendKind.Category"/>.</param>
/// <param name="Name">Nazwa kategorii produktu; <c>null</c> dla wierszy innych niż <see cref="ProductSpendKind.Category"/> — etykiety tłumaczy front.</param>
/// <param name="Amount">Kwota w oglądanym okresie.</param>
/// <param name="Kind">Rodzaj wiersza.</param>
public sealed record ProductSpendResponseDto(Guid? ProductCategoryId, string? Name, decimal Amount, ProductSpendKind Kind);
