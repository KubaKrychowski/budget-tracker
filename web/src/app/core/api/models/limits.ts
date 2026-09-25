import { BudgetOption } from './budget-option';

/** Odpowiednik LimitState z BudgetTracker.Api.Features.Limits.Consts — kolor paska. */
export type LimitState = 'Ok' | 'Warning' | 'Over';

/** Odpowiednik LimitRowResponseDto — wiersz tabeli limitów. */
export interface LimitRow {
  readonly id: string;
  readonly categoryId: string;
  readonly categoryName: string;
  readonly limit: number;
  readonly spent: number;
  /** Może być ujemne — to informacja, nie błąd. */
  readonly remaining: number;
  readonly percent: number;
  readonly warningThreshold: number;
  readonly state: LimitState;
  readonly validFrom: string;
  /** Kwota, która zastąpi ten limit w kolejnym miesiącu („od IX: 1 200 zł"), albo `null`. */
  readonly nextLimit: number | null;
  readonly nextValidFrom: string | null;
}

/** Kategoria z wydatkami, która nie ma limitu w oglądanym miesiącu. */
export interface UnlimitedCategory {
  readonly categoryId: string;
  readonly categoryName: string;
  readonly spent: number;
}

/** Kategoria do modala „Ustaw limit"; `hasLimit` = już ma limit w oglądanym miesiącu. */
export interface LimitCategoryOption {
  readonly id: string;
  readonly name: string;
  readonly hasLimit: boolean;
}

/** Odpowiednik LimitsResponseDto — cały ekran jednym żądaniem. */
export interface LimitsResponse {
  readonly month: string;
  readonly currentMonth: string;
  /** Miesiąc zamknięty — bez akcji zmieniających limity. */
  readonly readOnly: boolean;
  readonly limits: LimitRow[];
  readonly unlimited: UnlimitedCategory[];
  readonly categories: LimitCategoryOption[];
  /** Miesięczny limit budżetu = suma limitów obowiązujących w miesiącu. */
  readonly limitTotal: number;
  readonly spentInLimited: number;
  readonly spentOutside: number;
  readonly overLimitCount: number;
  /** Wydatki bez kategorii — nie liczą się do żadnego limitu. */
  readonly uncategorizedCount: number;
  readonly uncategorizedAmount: number;
  readonly selectedBudgetIds: string[];
  readonly budgets: BudgetOption[];
  /**
   * Budżet ma limit w JAKIMKOLWIEK miesiącu — nie tylko w oglądanym.
   *
   * Rozstrzyga, co powiedzieć przy pustej tabeli: „nie masz jeszcze żadnych limitów" czy
   * „w tym miesiącu ich nie ma, ale w innych są".
   */
  readonly hasAnyLimit: boolean;
}
