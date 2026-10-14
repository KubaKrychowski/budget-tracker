using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Import.Services;
using BudgetTracker.Api.Features.ProductCategories.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Tests;

/// <summary>Składanie kategoryzatora produktów dla testów handlerów tworzonych wprost (bez DI).</summary>
internal static class ProductTestKit
{
    public static ProductCategorizer Categorizer(AppDbContext db, Guid userId, decimal threshold = 0.7m) => new(
        new ProductRuleCategorizer(db),
        new ProductMlCategorizer(db, new FakeCurrentUserAccessor(userId), new MemoryCache(new MemoryCacheOptions())),
        new ImportConfidenceThreshold(Options.Create(new CategorizationOptions { ConfidenceThreshold = threshold })));
}
