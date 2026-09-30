using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// <see cref="SystemDb.EnterSystemRoleAsync"/> na PRAWDZIWYM Postgresie: rola omijająca RLS zostaje bez zmian, a zwykła
/// rola aplikacji (tor zapasowy) przechodzi w <c>budget_jobs</c> tylko na czas transakcji.
///
/// Wymaga `docker compose up -d db` oraz ról z api/db/setup-rls-roles.sql (CI zakłada je przed testami).
/// </summary>
public sealed class SystemDbTests
{
    private const string OwnerConnection =
        "Host=localhost;Port=5432;Database=postgres;Username=budget;Password=budget_dev_only";

    private const string AppConnection =
        "Host=localhost;Port=5432;Database=postgres;Username=budget_app;Password=budget_app_dev_only";

    private static AppDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    private static Task<string> CurrentRoleAsync(AppDbContext db) =>
        db.Database.SqlQueryRaw<string>("SELECT current_user AS \"Value\"").SingleAsync();

    [Fact]
    public async Task A_role_that_already_bypasses_rls_is_left_alone()
    {
        await using var db = Context(OwnerConnection);
        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.EnterSystemRoleAsync();

        Assert.Equal("budget", await CurrentRoleAsync(db));
    }

    [Fact]
    public async Task The_plain_app_role_falls_back_to_budget_jobs_only_inside_the_transaction()
    {
        await using var db = Context(AppConnection);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.EnterSystemRoleAsync();
            Assert.Equal("budget_jobs", await CurrentRoleAsync(db));
        }

        Assert.Equal("budget_app", await CurrentRoleAsync(db));
    }
}
