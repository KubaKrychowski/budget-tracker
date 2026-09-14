import { BudgetOption } from './budget-option';

/** Odpowiednik StandingOrderRhythm z BudgetTracker.Api.Domain.Consts. */
export type StandingOrderRhythm = 'Monthly' | 'Quarterly' | 'Yearly';

/** Odpowiednik StandingOrderMonthState — kolumna „W tym miesiącu”. */
export type StandingOrderMonthState = 'Paid' | 'PaidDifferentAmount' | 'Waiting' | 'Missed' | 'NotDue' | 'Ended';

/** Odpowiednik StandingOrderRuleRequestDto / StandingOrderRuleResponseDto — jedna reguła dopasowania. */
export interface StandingOrderRule {
  readonly titlePattern: string;
  readonly amountFrom: number;
  readonly amountTo: number;
}

/** Odpowiednik StandingOrderRowResponseDto — wiersz tabeli zleceń stałych. */
export interface StandingOrderRow {
  readonly id: string;
  readonly name: string;
  readonly expectedAmount: number;
  readonly rhythm: StandingOrderRhythm;
  /** Miesiąc 1–12 dla rocznego i kwartalnego; `null` przy miesięcznym. */
  readonly dueMonth: number | null;
  /** Reguły łączy „lub” — transakcja należy do zlecenia, gdy pasuje do którejkolwiek. */
  readonly rules: StandingOrderRule[];
  /** Ostatni miesiąc zlecenia (pierwszy dzień); `null` = trwa. */
  readonly endMonth: string | null;
  /** Najczęstsza kategoria przypiętych transakcji — zlecenie nie ma własnej. */
  readonly categoryName: string | null;
  readonly state: StandingOrderMonthState;
  readonly paidOn: string | null;
  readonly paidAmount: number | null;
  /** Mediana dnia z historii — „zwykle do 15.”. */
  readonly usualDay: number | null;
  readonly linkedCount: number;
}

/** Ostatnio przypięta transakcja. */
export interface StandingOrderPin {
  readonly transactionId: string;
  readonly standingOrderId: string;
  readonly standingOrderName: string;
  readonly date: string;
  readonly amount: number;
  readonly differentAmount: boolean;
}

/** Odpowiednik StandingOrdersResponseDto — cały ekran jednym żądaniem. */
export interface StandingOrdersResponse {
  readonly month: string;
  readonly currentMonth: string;
  readonly orders: StandingOrderRow[];
  readonly recent: StandingOrderPin[];
  /** Stałe w przeliczeniu na miesiąc: kwartalne ÷ 3, roczne ÷ 12. Zakończone się nie liczą. */
  readonly monthlyTotal: number;
  readonly dueCount: number;
  readonly paidCount: number;
  readonly waitingAmount: number;
  readonly differentAmountCount: number;
  readonly selectedBudgetIds: string[];
  readonly budgets: BudgetOption[];
}

/** Wynik podglądu reguł przed zapisem. */
export interface StandingOrderPreview {
  readonly matchCount: number;
  /** Pasujące, ale przypięte już do innego zlecenia — nie przejdą. */
  readonly takenByOtherOrders: number;
  readonly lastDate: string | null;
  readonly lastAmount: number | null;
}
