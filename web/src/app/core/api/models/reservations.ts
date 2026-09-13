import { BudgetOption } from './budget-option';

/** Odpowiednik ReservationStatus z BudgetTracker.Api.Features.Savings.Consts — trzy statusy z makiety. */
export type ReservationStatus = 'Collecting' | 'Overdue' | 'Settled';

/** Odpowiednik SavingsReservationView — wiersz tabeli i jednocześnie jeden pierścień w kaflu. */
export interface SavingsReservation {
  readonly id: string;
  readonly name: string;
  readonly amount: number;
  /** Pierwszy dzień miesiąca terminu (ISO). */
  readonly dueMonth: string;
  /**
   * Ile z tej rezerwacji jest naprawdę pokryte pieniędzmi na koncie — przydział z kolejki
   * (najbliższy termin pierwszy), nie procent od oka. Rozliczona ma tu pełną kwotę.
   */
  readonly collected: number;
  readonly status: ReservationStatus;
  /** Data wskazanej wypłaty; `null`, gdy nierozliczona. */
  readonly settledOn: string | null;
  readonly settledTransactionId: string | null;
}

/** Odpowiednik SavingsReservationsResponse. */
export interface ReservationsResponse {
  readonly reservations: SavingsReservation[];
  /**
   * Stan konta oszczędnościowego. ⚠️ Przed #10 to FALLBACK: wpłaty minus wypłaty w kategorii
   * „Oszczędności", czyli „ile netto przesunąłeś", a nie saldo konta — bez salda otwarcia
   * i bez odsetek. Ekran musi to powiedzieć wprost.
   */
  readonly accountBalance: number;
  /** Suma kwot WSZYSTKICH rezerwacji, także rozliczonych. */
  readonly reservedTotal: number;
  readonly settledTotal: number;
  readonly collectedTotal: number;
  /**
   * `accountBalance − reservedTotal`. Może być ujemne — wtedy ekran pokazuje osobny stan
   * „rezerwacje przekraczają stan konta o X", a nie minus w kaflu.
   *
   * ⚠️ Rozliczenie tej liczby NIE zmienia i to jest decyzja, nie przeoczenie: koperta jest
   * ROCZNA (makieta 147:96 odejmuje też rozliczoną rezerwację). Ekran musi to wytłumaczyć,
   * inaczej użytkownik rozliczy ubezpieczenie, zobaczy zero zmiany i uzna to za błąd.
   */
  readonly freeFunds: number;
  /**
   * Miesiąc, w którym przy obecnym celu uzbiera się reszta rezerwacji.
   * `null`, gdy wszystko pokryte albo gdy nie ma celu — bez tempa to byłaby zgadywanka.
   */
  readonly coveredBy: string | null;
  readonly selectedBudgetIds: string[];
  /** Budżety do przełącznika nad listą — wyłączone też, z tagiem. */
  readonly budgets: BudgetOption[];
}

/** Kandydat do rozliczenia — „czy to było ubezpieczenie?". */
export interface SettleCandidate {
  readonly id: string;
  readonly date: string;
  readonly amount: number;
  readonly description: string;
}
