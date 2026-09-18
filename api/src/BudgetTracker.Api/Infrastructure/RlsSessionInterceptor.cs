using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Ustawia zmienną sesyjną Postgresa <c>app.current_user_id</c> na każdym otwarciu połączenia —
/// polityki RLS (row-level security) na <see cref="Domain.Budget"/> i jego dzieciach porównują
/// z nią kolumnę <c>UserId</c>, bo appka łączy się JEDNĄ wspólną rolą dla wszystkich użytkowników
/// (nie ma osobnej roli Postgresa per konto).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Nie <c>SET LOCAL</c> — to zwykłe <c>SET</c> (przez <c>set_config(..., is_local: false)</c>),
/// bo EF Core otwiera połączenie RAZ na cały zakres <see cref="AppDbContext"/> (czyli na całe
/// żądanie HTTP), nie per zapytanie. Działa to bezpiecznie z poolingiem Npgsql: ten interceptor
/// odpala się przy KAŻDYM <c>Open()</c>, także dla połączenia odzyskanego z puli, więc kolejny
/// najemca zawsze nadpisuje wartość poprzedniego, zanim cokolwiek zapyta.
/// </para>
/// <para>
/// Poza kontekstem żądania HTTP (testy, seedy, Hangfire) <see cref="ICurrentUserAccessor.UserId"/>
/// jest <c>null</c> — ustawiamy wtedy pusty string, żeby <c>current_setting(..., true)</c> w polityce
/// dał NULL zamiast poprzedniej wartości z puli połączeń. Te ścieżki i tak omijają RLS przez rolę
/// z <c>BYPASSRLS</c> (<c>budget_jobs</c>, patrz <see cref="Features.Budgets.Services.BudgetPurger"/>)
/// albo działają na bazie testowej bez RLS w ogóle.
/// </para>
/// </remarks>
public sealed class RlsSessionInterceptor(ICurrentUserAccessor currentUser) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetSessionUser(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default)
    {
        await SetSessionUserAsync(connection, ct);
        await base.ConnectionOpenedAsync(connection, eventData, ct);
    }

    private void SetSessionUser(DbConnection connection)
    {
        using var command = BuildCommand(connection);
        command.ExecuteNonQuery();
    }

    private async Task SetSessionUserAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = BuildCommand(connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    private NpgsqlCommand BuildCommand(DbConnection connection)
    {
        var command = ((NpgsqlConnection)connection).CreateCommand();
        command.CommandText = "SELECT set_config('app.current_user_id', @value, false)";
        command.Parameters.AddWithValue("value", currentUser.UserId?.ToString() ?? "");
        return command;
    }
}
