namespace BudgetTracker.Identity.Models;

/// <summary>
/// Adres, któremu wolno założyć konto w zamkniętej becie.
/// </summary>
/// <remarks>
/// <para>
/// Lista żyje w BAZIE, a nie w konfiguracji jak <see cref="Options.AdminOptions"/>, bo zaprasza się
/// w trakcie działania serwera: dopisanie adresu nie może wymagać restartu ani dostępu do sekretów.
/// </para>
/// <para>
/// ⚠️ Adres jest tu JAWNY, w przeciwieństwie do dziennika audytu, który trzyma sam skrót. To konieczne —
/// ekran ma pokazać, kogo się zaprosiło, a skrótu nie da się odczytać. Ta tabela jest więc zbiorem adresów
/// e-mail osób, które nie mają jeszcze konta: traktuj ją jak dane osobowe. Po becie ma zostać skasowana,
/// a nie „zostawiona na później".
/// </para>
/// <para>
/// ⚠️ Wpis NIE jest kontem i nie daje dostępu — pozwala wyłącznie przejść rejestrację. Skasowanie wpisu
/// nie kasuje konta, które na jego podstawie powstało (to jest w „Użytkownicy").
/// </para>
/// </remarks>
public sealed class BetaInvite
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Adres po <see cref="Normalize"/> — w bazie leży wyłącznie postać znormalizowana.</summary>
    public required string Email { get; init; }

    public DateTimeOffset AddedAt { get; init; }

    /// <summary>Administrator, który dopisał adres. Jego konto może później zniknąć, więc trzymamy sam identyfikator.</summary>
    public Guid AddedByUserId { get; init; }

    /// <summary>
    /// Postać do porównań i zapisu. Bez niej „Jan@Example.COM" z zaproszenia i „jan@example.com" z formularza
    /// rejestracji byłyby dwoma różnymi adresami, czyli zaproszony dostałby odmowę za wielkość liter.
    /// </summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
