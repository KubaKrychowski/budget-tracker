namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>
/// Zasięg akcji masowej. <see cref="Filter"/> jest OBOWIĄZKOWY i wyznacza wszechświat operacji
/// (przede wszystkim budżet); <see cref="Ids"/> tylko go ZAWĘŻA do wskazanych wierszy.
/// </summary>
/// <remarks>
/// ⚠️ Same <see cref="Ids"/>, bez filtra, byłyby dziurą: klient mógłby podać identyfikatory
/// z zupełnie innego budżetu niż ten, który ma przed oczami, a serwer nie miałby ich do czego
/// przyłożyć. Realny scenariusz: zaznaczenie wierszy, potem cofnięcie w przeglądarce na ten
/// sam ekran z innym `budgetId` — front zostawał ze starymi identyfikatorami. Front to teraz
/// czyści (patrz `Transactions.clearSelection`), ale serwer i tak sprawdza sam, bo jeden
/// z dwóch bezpieczników zawsze kiedyś wypadnie.
///
/// Brak filtra = akcja nie dotyka żadnego wiersza, zamiast zgadywać intencję.
/// </remarks>
public sealed record TransactionSelectionRequestDto(IReadOnlyList<Guid>? Ids, TransactionFilterRequestDto? Filter);
