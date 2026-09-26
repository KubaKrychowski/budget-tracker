using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Ustawia zmienną sesyjną Postgresa <c>app.current_user_id</c> przez <c>SET LOCAL</c> (transakcyjnie) na
/// początku KAŻDEJ transakcji. To jest naprawa działająca za poolerem Neona (PgBouncer w trybie transakcyjnym).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Dlaczego transakcyjnie, a nie sesyjnie (<see cref="RlsSessionInterceptor"/>): baza jest za endpointem
/// <c>-pooler</c> Neona = PgBouncer w trybie transakcyjnym. Tam połączenie klienta jest przypięte do konkretnego
/// backendu Postgresa TYLKO na czas transakcji. Sesyjne <c>SET</c> (<c>is_local=false</c>) ustawione poza
/// transakcją ląduje na jednym backendzie i znika, gdy backend wraca do puli — kolejne polecenie (osobna
/// transakcja) trafia na inny backend BEZ tej zmiennej, więc polityka RLS <c>current_setting('app.current_user_id')</c>
/// nie pasuje i wycina wiersze użytkownika. Objaw: dane raz są, raz „znikają".
/// </para>
/// <para>
/// <c>SET LOCAL</c> (<c>is_local=true</c>) żyje w obrębie transakcji, a w trybie transakcyjnym PgBouncer trzyma
/// jeden backend przez całą transakcję — więc <c>SET LOCAL</c> i zapytania są na TYM SAMYM backendzie, a zmienna
/// sama się czyści przy <c>COMMIT</c> (brak wycieku do następnego najemcy puli). Odczyty dostają transakcję z
/// <see cref="RlsTransactionEndpointFilter"/>; zapisy mają własne transakcje (reużywają transakcji otoczenia).
/// </para>
/// <para>
/// Poza żądaniem (Hangfire, seedy) <see cref="ICurrentUserAccessor.UserIdOrNull"/> jest <c>null</c> — ustawiamy
/// pusty string, żeby <c>current_setting(..., true)</c> dał NULL. Te ścieżki i tak działają na roli
/// <c>budget_jobs</c> (BYPASSRLS).
/// </para>
/// </remarks>
public sealed class RlsTransactionInterceptor(ICurrentUserAccessor currentUser) : DbTransactionInterceptor
{
    public override DbTransaction TransactionStarted(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        using var command = BuildCommand(connection, result);
        command.ExecuteNonQuery();
        return base.TransactionStarted(connection, eventData, result);
    }

    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        await using var command = BuildCommand(connection, result);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await base.TransactionStartedAsync(connection, eventData, result, cancellationToken);
    }

    private NpgsqlCommand BuildCommand(DbConnection connection, DbTransaction transaction)
    {
        var command = ((NpgsqlConnection)connection).CreateCommand();
        command.Transaction = (NpgsqlTransaction)transaction;
        command.CommandText = "SELECT set_config('app.current_user_id', @value, true)";
        command.Parameters.AddWithValue("value", currentUser.UserIdOrNull?.ToString() ?? "");
        return command;
    }
}
