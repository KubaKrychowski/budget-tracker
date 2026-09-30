import { BudgetOption } from './budget-option';

/** Odpowiednik ReservationStatus z BudgetTracker.Api.Features.Savings.Consts — trzy statusy z makiety. */
export type ReservationStatus = 'Collecting' | 'Overdue' | 'Settled';

/** Odpowiednik SavingsReservationView — wiersz tabeli i jednocześnie jeden pierścień w kaflu. */
export interface SavingsReservation {
  readonly id: string;
  readonly name: string;
  readonly amount: number;
  /** Pierwszy dzień miesiąca terminu (ISO); `null` = „przy okazji”. */
  readonly dueMonth: string | null;
  /** Suma wpłat na cel (zgłoszenie #23). Rozliczona ma tu pełną kwotę. */
  readonly collected: number;
  readonly status: ReservationStatus;
  /** Data wskazanej wypłaty; `null`, gdy nierozliczona. */
  readonly settledOn: string | null;
  readonly settledTransactionId: string | null;
  /** Wpłaty od najnowszej — lista z „Wycofaj” w dialogu wpłaty. */
  readonly contributions: SavingsContribution[];
}

/** Odpowiednik ContributionSource z BudgetTracker.Api.Domain.Consts — konto, z którego odkładasz na cel. */
export type ContributionSource = 'Savings' | 'Regular';

/** Umowna wpłata na cel — pieniądze zostają na koncie, z którego odkładasz. */
export interface SavingsContribution {
  readonly id: string;
  readonly date: string;
  readonly amount: number;
  readonly source: ContributionSource;
  /** Kategoria limitu wpłaty ze zwykłego konta; `null` dla oszczędności. */
  readonly categoryId: string | null;
}

/** Kategoria do wyboru przy wpłacie ze zwykłego konta — z limitem i wydanym w bieżącym okresie budżetu. */
export interface ContributionCategory {
  readonly id: string;
  readonly name: string;
  /** `null`, gdy kategoria nie ma w tym okresie limitu. */
  readonly limit: number | null;
  /** Wydane w okresie razem z wpłatami na cele ze zwykłego konta. */
  readonly spent: number;
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
  /** Suma kwot rezerwacji NIEROZLICZONYCH. */
  readonly reservedTotal: number;
  readonly settledTotal: number;
  /** Suma wpłat na rezerwacje nierozliczone. */
  readonly collectedTotal: number;
  /** Ile jeszcze da się wpłacić na cele: stan konta minus wpłaty na nierozliczone. */
  readonly availableToContribute: number;
  /**
   * `accountBalance − reservedTotal`. Może być ujemne — wtedy ekran pokazuje osobny stan
   * „rezerwacje przekraczają stan konta o X", a nie minus w kaflu.
   *
   * Rozliczona rezerwacja przestaje ją pomniejszać (zgłoszenie #23) — zapłacony zakup zszedł już ze stanu konta.
   */
  readonly freeFunds: number;
  /** Wpłaty ze zwykłego konta na rezerwacje nierozliczone — tyle na zwykłym koncie jest zarezerwowane na cele. */
  readonly reservedOnRegular: number;
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
