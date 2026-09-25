using Azure.Storage.Blobs;
using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Categorization.Queries;

/// <summary>Ekran „Dane treningowe": skład zbioru, rozkład po kategoriach, kolejka przeglądu i wersje modelu.</summary>
public sealed class GetTrainingSetQueryHandler(AppDbContext db, TrainingSetBuilder builder, ICurrentUserAccessor currentUserAccessor)
{
    public async Task<TrainingSetOverviewResponseDto> HandleAsync(CancellationToken ct)
    {
        var set = await builder.BuildAsync(ct);

        var pendingReview = await db.Transactions
            .CountAsync(t => t.Status == TransactionStatus.PendingReview, ct);

        var userId = currentUserAccessor.UserId;

        var versions = await db.Set<ModelVersion>()
            .ToListAsync(ct);

        var last = versions.FirstOrDefault(v => v.Active);
        var sinceLast = last is null
            ? set.Composition.FromCorrections + set.Composition.Corrected
            : set.CorrectionsNewerThan(last.CreatedAt.GetValueOrDefault());

        return new TrainingSetOverviewResponseDto(
            set.Composition,
            CountByCategory(set, await db.Categories.Select(c => c.Name).ToListAsync(ct)),
            pendingReview,
            sinceLast,
            versions.Where(v => v.UserId == userId)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new ModelVersionResponseDto(
                    v.BusinessId,
                    v.Name,
                    v.CreatedAt.GetValueOrDefault(),
                    v.Active,
                    new TrainingReportResponseDto(
                        v.TrainingSet,
                        v.CategoriesCount,
                        (double)v.Accuracy,
                        (double)v.AverageAccuracy)))
                .ToList());
    }
    
    private static IReadOnlyList<CategoryExampleCountResponseDto> CountByCategory(
        TrainingSet set, IReadOnlyList<string> allCategories)
    {
        var counts = set.Rows
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var names = allCategories
            .Concat(counts.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. names
                .Select(name => new CategoryExampleCountResponseDto(name, counts.GetValueOrDefault(name)))
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.Name, StringComparer.CurrentCulture)
        ];
    }
}