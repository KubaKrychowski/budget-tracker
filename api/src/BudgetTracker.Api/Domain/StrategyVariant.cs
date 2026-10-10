namespace BudgetTracker.Api.Domain;

/// <summary>
/// Wariant strategii — ten sam graf z wyłączonymi kafelkami, zapisany w kolumnie <c>jsonb</c> razem ze strategią.
/// </summary>
/// <remarks>
/// <para>
/// Wariant nie ma własnego grafu ani własnych parametrów kafelków: to tylko lista kafelków, które symulacja ma pominąć.
/// Dzięki temu zmiana kwoty w kafelku zmienia wynik WSZYSTKICH wariantów, a „Bazowy” (brak wyłączeń) nie jest zapisywany
/// — istnieje zawsze.
/// </para>
/// <para>
/// ⚠️ Wyłączony kafelek zostaje w strategii i zachowuje połączenia, ale nie wykonuje się, więc nie wykonują się też
/// kafelki, do których prowadzi wyłącznie on (łańcuch po wyłączonym zdarzeniu).
/// </para>
/// </remarks>
/// <param name="Id">Identyfikator wariantu nadany przez klienta, stabilny między zapisami.</param>
/// <param name="Name">Nazwa wariantu widoczna na pasku wariantów.</param>
/// <param name="DisabledNodeIds">Identyfikatory kafelków, które w tym wariancie są wyłączone.</param>
public sealed record StrategyVariant(string Id, string Name, List<string> DisabledNodeIds);
