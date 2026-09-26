using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Usuwanie i odtwarzanie baz testowych — jedna droga dla wszystkich klas testowych.
/// </summary>
/// <remarks>
/// <para>
/// Wcześniej każda klasa robiła to sama przez <c>EnsureDeletedAsync</c> + <c>EnsureCreatedAsync</c>. Wyłączenie
/// równoległości (<c>AssemblyInfo.cs</c>) usunęło wyścig MIĘDZY klasami, ale zostały trzy mechanizmy, które dawały
/// ten sam objaw — rzadki, niepowtarzalny błąd infrastruktury, zwykle z wyraźnie dłuższym przebiegiem:
/// </para>
/// <list type="number">
/// <item>⚠️ <b>Martwe połączenie z puli.</b> Npgsql trzyma połączenia w puli per connection string. Usunięcie bazy
/// i odtworzenie jej pod TĄ SAMĄ nazwą zostawia w puli połączenie do bazy, której już nie ma — następne wypożyczenie
/// kończy się „terminating connection due to administrator command". Dlatego po każdym usunięciu czyścimy pule.</item>
/// <item><b>Obca sesja na usuwanej bazie.</b> <c>DROP DATABASE</c> odmawia, gdy ktoś jest podłączony (narzędzie
/// bazodanowe w IDE, <c>psql</c>, niezamknięte API). <c>WITH (FORCE)</c> zamyka te sesje.</item>
/// <item><b>Obca sesja na <c>template1</c>.</b> <c>CREATE DATABASE</c> kopiuje domyślnie <c>template1</c> i odmawia,
/// gdy ktoś jest do niego podłączony — a przeglądarki baz w IDE potrafią to robić. <c>TEMPLATE template0</c> omija
/// problem, bo do <c>template0</c> nie da się podłączyć.</item>
/// </list>
/// <para>
/// ⚠️ Gdy mimo to się nie uda, wyjątek mówi, KTO trzymał bazę. Poprzedni flake przepadł bez śladu, bo komunikat
/// nie przetrwał — i nie dało się ustalić przyczyny. Ten tekst ma wystarczyć bez ponownego uruchamiania.
/// </para>
/// </remarks>
internal static class TestDatabase
{
    private const int Attempts = 3;

    /// <summary>
    /// Kody Postgresa, przy których ponowienie ma sens: obiekt w użyciu, sesja zamknięta przez administratora,
    /// za dużo klientów. Wszystko inne (np. brak uprawnień) jest błędem konfiguracji i ma wyjść od razu.
    /// </summary>
    private static readonly HashSet<string> Transient = ["55006", "57P01", "53300"];

    /// <summary>Usuwa bazę testową (jeśli jest) i tworzy ją od nowa, pustą — tabele dokłada potem <c>EnsureCreatedAsync</c>.</summary>
    public static async Task ResetAsync(string connectionString)
    {
        var name = DatabaseName(connectionString);
        await ExecuteAsync(connectionString, name, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        NpgsqlConnection.ClearAllPools();
        await ExecuteAsync(connectionString, name, $"CREATE DATABASE \"{name}\" TEMPLATE template0");
        await GrantToAppRolesAsync(connectionString);
    }

    /// <summary>
    /// Zapowiada uprawnienia dla <c>budget_app</c> i <c>budget_jobs</c> na tabelach, których JESZCZE NIE MA.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Bez tego <see cref="Features.Budgets.Services.BudgetPurger"/> pada na „42501: permission denied for
    /// table Budgets". Rola <c>budget_jobs</c> istnieje (zakłada ją <c>api/db/setup-rls-roles.sql</c>), ale nie
    /// jest właścicielem tabel, więc bez GRANT-ów nie ma do czego sięgnąć — a wchodzi się w nią przez
    /// <c>SET LOCAL ROLE</c>, więc nie pomaga to, że sama appka łączy się rolą uprzywilejowaną.
    /// </para>
    /// <para>
    /// Uprawnienia nadajemy ZAPOWIEDZIĄ (<c>ALTER DEFAULT PRIVILEGES</c>), a nie <c>ON ALL TABLES</c>, bo w tym
    /// momencie baza jest pusta — tabele dokłada dopiero <c>EnsureCreatedAsync</c>/<c>MigrateAsync</c> klasy testowej.
    /// </para>
    /// <para>
    /// ⚠️ Nie da się tego załatwić raz, na szablonie: bazy testowe powstają z <c>template0</c> (patrz
    /// <see cref="ResetAsync"/>), więc nie dziedziczą niczego, co dopisano do <c>template1</c>.
    /// </para>
    /// </remarks>
    private static async Task GrantToAppRolesAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            GRANT USAGE ON SCHEMA public TO budget_app, budget_jobs;

            ALTER DEFAULT PRIVILEGES IN SCHEMA public
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO budget_app, budget_jobs;

            ALTER DEFAULT PRIVILEGES IN SCHEMA public
                GRANT USAGE, SELECT ON SEQUENCES TO budget_app, budget_jobs;
            """,
            connection);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Usuwa bazę testową, jeśli istnieje.</summary>
    public static async Task DropAsync(string connectionString)
    {
        var name = DatabaseName(connectionString);
        await ExecuteAsync(connectionString, name, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        NpgsqlConnection.ClearAllPools();
    }

    /// <remarks>
    /// Nazwa trafia do SQL przez interpolację — identyfikatorów nie da się przekazać parametrem. Jest stałą w kodzie
    /// testu, ale i tak ją sprawdzamy, bo pomyłka w nazwie mogłaby wskazać bazę, której testy nie powinny ruszać.
    /// </remarks>
    private static string DatabaseName(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database
            ?? throw new InvalidOperationException("Connection string testów nie wskazuje bazy.");

        if (!Regex.IsMatch(name, "^budgettracker_[a-z0-9_]+_test$"))
        {
            throw new InvalidOperationException(
                $"Baza testowa musi się nazywać budgettracker_<cos>_test, a jest „{name}\". " +
                "To zabezpieczenie przed skasowaniem bazy deweloperskiej przez pomyłkę w connection stringu.");
        }

        return name;
    }

    /// <summary>
    /// Wykonuje polecenie administracyjne na bazie <c>postgres</c>, bez puli — połączenie administracyjne samo
    /// nie może stać się martwym połączeniem w puli.
    /// </summary>
    private static async Task ExecuteAsync(string connectionString, string name, string sql)
    {
        var admin = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", Pooling = false };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(admin.ConnectionString);
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();
                return;
            }
            catch (PostgresException ex) when (Transient.Contains(ex.SqlState) && attempt < Attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt));
            }
            catch (PostgresException ex)
            {
                throw new InvalidOperationException(
                    $"Infrastruktura testów, NIE logika: nie udało się wykonać „{sql}\" " +
                    $"(SQLSTATE {ex.SqlState}: {ex.MessageText}) po {attempt} próbach.\n" +
                    await DescribeSessionsAsync(admin.ConnectionString, name), ex);
            }
        }
    }

    /// <summary>Kto był podłączony do bazy testowej i do <c>template1</c> w chwili błędu.</summary>
    private static async Task<string> DescribeSessionsAsync(string adminConnectionString, string name)
    {
        try
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                select datname, pid, coalesce(application_name, ''), coalesce(client_addr::text, 'lokalnie'), state, backend_start
                from pg_stat_activity
                where datname in (@name, 'template1') and pid <> pg_backend_pid()
                order by datname, backend_start
                """, connection);
            command.Parameters.AddWithValue("name", name);

            var text = new StringBuilder("Sesje w chwili błędu:\n");
            await using var reader = await command.ExecuteReaderAsync();
            var any = false;
            while (await reader.ReadAsync())
            {
                any = true;
                text.AppendLine(
                    $"  {reader.GetString(0)} pid={reader.GetInt32(1)} app=„{reader.GetString(2)}\" " +
                    $"z={reader.GetString(3)} stan={reader.GetValue(4)} od={reader.GetValue(5)}");
            }

            return any ? text.ToString() : "Sesje w chwili błędu: brak obcych sesji na tej bazie ani na template1.";
        }
        catch (Exception ex)
        {
            return $"Nie udało się odczytać sesji: {ex.Message}";
        }
    }
}
