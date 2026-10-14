using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.ProductCategories.Exceptions;
using BudgetTracker.Api.Features.ProductCategories.Services;
using BudgetTracker.Api.Features.Receipts.Contracts;
using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Receipts.Commands;

/// <summary>Ustawia kategorię produktu jednej pozycji paragonu i zapamiętuje wybór jako regułę dla tej nazwy.</summary>
/// <remarks>
/// <para>
/// Pozycja jest adresowana INDEKSEM, bo leży w kolumnie <c>jsonb</c> bez własnego identyfikatora — lista nie zmienia kolejności po
/// zapisie (<see cref="Receipt.SetItems"/> woła się raz, przy wgraniu), więc indeks z odpowiedzi API pozostaje ważny.
/// </para>
/// <para>
/// Wybór użytkownika oznacza pozycję jako potwierdzoną (uczy model) i zapisuje regułę <c>^nazwa$</c> — kolejny paragon z tym produktem
/// dostanie kategorię sam. Zdjęcie kategorii kasuje regułę nauczoną dla tej nazwy. Kategoria pozycji NIE zmienia kwoty ani kategorii
/// transakcji (DECISIONS.md §17).
/// </para>
/// </remarks>
public sealed class SetReceiptLineCategoryCommandHandler(AppDbContext db, ProductRuleLearning learning)
{
    public async Task<ReceiptLineResponseDto> HandleAsync(
        Guid receiptId, int index, SetReceiptLineCategoryRequestDto request, CancellationToken ct)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.BusinessId == receiptId, ct)
            ?? throw new ReceiptNotFoundException(receiptId);
        if (index < 0 || index >= receipt.Items.Count) throw new ReceiptItemNotFoundException();

        ProductCategory? category = null;
        if (request.ProductCategoryId is { } categoryId)
        {
            category = await db.ProductCategories.FirstOrDefaultAsync(c => c.BusinessId == categoryId, ct)
                ?? throw new ProductCategoryInvalidException();
        }

        var line = receipt.SetItemCategory(index, request.ProductCategoryId)!;
        await learning.LearnAsync(line.Name, category, ct);
        await db.SaveChangesAsync(ct);

        return new ReceiptLineResponseDto(
            line.Name, line.Quantity, line.UnitPrice, line.TotalPrice, line.ProductCategoryId, line.CategoryConfirmed);
    }
}
