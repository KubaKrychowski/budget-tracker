namespace BudgetTracker.Identity.Models;

/// <summary>
/// Prośba o dostęp do zamkniętej bety, zostawiona na stronie projektu (landing).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ To zbiór adresów e-mail osób, które nie mają konta — dane osobowe, podane za zgodą. Zgoda jest udokumentowana
/// wersją dokumentów (<see cref="ConsentVersion"/>) i datą, bo bez tego „zgodziłem się" nie da się udowodnić.
/// Prośba NIE daje dostępu: zaproszenie to osobna decyzja administratora (<see cref="BetaInvite"/>).
/// </para>
/// <para>
/// Prośba zostaje po zaproszeniu (<see cref="InvitedAt"/>), żeby ekran pokazywał, kto już dostał zaproszenie,
/// i żeby to samo zgłoszenie nie wracało jako „nowe". Usuwa się ją ręcznie w panelu administratora.
/// </para>
/// </remarks>
public sealed class BetaAccessRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Adres po <see cref="BetaInvite.Normalize"/> — ta sama postać co w zaproszeniach, żeby dało się je porównać.</summary>
    public required string Email { get; init; }

    public DateTimeOffset RequestedAt { get; init; }

    /// <summary>Wersja Regulaminu i Polityki prywatności, które użytkownik zaakceptował, zapisana po stronie SERWERA.</summary>
    public required string ConsentVersion { get; init; }

    /// <summary>Kiedy administrator zaprosił ten adres; <c>null</c> = prośba czeka.</summary>
    public DateTimeOffset? InvitedAt { get; set; }
}
