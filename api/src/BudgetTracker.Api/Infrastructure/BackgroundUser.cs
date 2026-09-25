namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Użytkownik, „w imieniu którego” działa kod poza żądaniem HTTP — zadania Hangfire i CLI.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Powód istnienia: <see cref="RlsSessionInterceptor"/> ustawia <c>app.current_user_id</c> z
/// <see cref="ICurrentUserAccessor"/> przy KAŻDYM otwarciu połączenia. W zadaniu w tle nie ma
/// <c>HttpContext</c>, więc bez tego mechanizmu zmienna sesyjna byłaby pusta: polityki RLS nie
/// pokazałyby ani jednego wiersza, a zapis odbiłby się od <c>WITH CHECK</c>. Objawem byłoby
/// „trening na pustym zbiorze”, a nie błąd uprawnień.
/// </para>
/// <para>
/// ⚠️ To jest JEDYNE miejsce, w którym da się obejść „użytkownik pochodzi z tokenu”. Ma być go widać
/// w przeglądzie kodu — dlatego jest osobnym typem z jawnym <c>using</c>, a nie polem ustawianym gdzieś
/// po drodze. Identyfikator ZAWSZE pochodzi z argumentu zadania, nigdy z danych, które zadanie przetwarza.
/// </para>
/// <para>
/// <c>AsyncLocal</c>, bo zakres musi przeżyć <c>await</c> i nie może wyciec do innego zadania
/// obsługiwanego równolegle przez tę samą pulę wątków.
/// </para>
/// </remarks>
public static class BackgroundUser
{
    private static readonly AsyncLocal<Guid?> Current = new();

    /// <summary>Identyfikator ustawiony przez <see cref="Use"/>, albo <c>null</c> poza takim zakresem.</summary>
    public static Guid? UserId => Current.Value;

    /// <summary>Zakres „działaj jako ten użytkownik”. Zwolnienie przywraca poprzedni stan.</summary>
    public static IDisposable Use(Guid userId)
    {
        var previous = Current.Value;
        Current.Value = userId;
        return new Scope(() => Current.Value = previous);
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) onDispose();
        }
    }
}
