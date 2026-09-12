using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Commands;

/// <summary>Masowe ustawienie kategorii transakcji z zasięgu zaznaczenia albo filtra.</summary>
/// <remarks>
/// Liczy się jako ręczna korekta człowieka — status i pewność ustawia <see cref="ManualCategoryCorrection"/>,
/// tak samo jak przy edycji inline i imporcie. Nieznana kategoria = 404, nie cicha zmiana na pustą.
/// </remarks>
public sealed class BulkSetTransactionCategoryCommandHandler(AppDbContext db, TransactionSelectionResolver selection)
{
    public async Task<BulkActionResponseDto> HandleAsync(BulkSetCategoryRequestDto request, CancellationToken ct)
    {
        var ids = await selection.ResolveAsync(request.Selection, ct);
        if (ids.Count == 0) return new BulkActionResponseDto(0);

        var categoryKey = await db.Categories
            .Where(c => c.BusinessId == request.CategoryId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new CategoryNotFoundException(request.CategoryId);

        var affected = await db.Transactions
            .Where(t => ids.Contains(t.BusinessId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.CategoryId, categoryKey)
                .SetProperty(t => t.Status, ManualCategoryCorrection.Status)
                .SetProperty(t => t.Confidence, ManualCategoryCorrection.Confidence), ct);

        return new BulkActionResponseDto(affected);
    }
}
