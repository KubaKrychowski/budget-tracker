import { BudgetOption } from './budget-option';

/** Odpowiednik MonthVerdict z BudgetTracker.Api.Features.Savings.Consts. */
export type MonthVerdict = 'NoGoal' | 'GoalMissed' | 'GoalMet' | 'Proof';

/** Odpowiednik SavingsMonth — wiersz tabeli i jednocześnie punkt wykresu. */
export interface SavingsMonth {
  /** Pierwszy dzień miesiąca (ISO). */
  readonly month: string;
  /** Ile odłożono. Suma WPŁAT, nie netto. */
  readonly deposited: number;
  /**
   * Ile wypłacono z oszczędności — osobna liczba, nigdy odejmowana od wpłat.
   * Jest po to, żeby uzasadnioną wypłatę (ubezpieczenie) dało się odróżnić od przelewu
   * tam i z powrotem: jedno i drugie widać w wierszu, a werdykt liczy się tylko z wpłat.
   */
  readonly withdrawn: number;
  /** Cel obowiązujący W TYM miesiącu; `null`, gdy wtedy celu nie było. */
  readonly goal: number | null;
  readonly oneOffTotal: number;
  readonly oneOffCount: number;
  readonly verdict: MonthVerdict;
}

export interface SavingsGoalView {
  readonly id: string;
  readonly amount: number;
  /** Pierwszy miesiąc obowiązywania (ISO). */
  readonly startedOn: string;
}

/**
 * Propozycja podniesienia celu — jedyna wypowiedź tego ekranu o przyszłości.
 * `null`, dopóki nie ma dwóch dowodów.
 */
export interface RaiseGoalSuggestion {
  /** Zapas ponad cel: NAJMNIEJSZY jednorazowy wydatek z miesięcy-dowodów, nie średnia. */
  readonly headroom: number;
  readonly suggestedAmount: number;
}

/** Odpowiednik SavingsResponse — wszystko, czego potrzebuje ekran, jednym żądaniem. */
export interface SavingsResponse {
  readonly goal: SavingsGoalView | null;
  /**
   * Historia miesięcy, od najnowszego. Te same liczby zasilają tabelę I wykres —
   * próg wykresu to `goal` per miesiąc, czyli SCHODEK, a nie jedna prosta.
   */
  readonly months: SavingsMonth[];
  /** Zawsze obecny, także pusty — kafel „odłożone w tym miesiącu" musi mieć co pokazać. */
  readonly currentMonth: SavingsMonth;
  readonly proofCount: number;
  readonly depositedThisYear: number;
  readonly raiseSuggestion: RaiseGoalSuggestion | null;
  /** `false` włącza stan „Brak oznaczonych wydatków" — bez nich dowód nie ma jak powstać. */
  readonly hasAnyLargeExpense: boolean;
  /** `false` przy istniejącym celu znaczy najczęściej: przelew nie trafił w kategorię. */
  readonly hasAnySavings: boolean;
  readonly selectedBudgetIds: string[];
  /** Budżety do przełącznika nad ekranem — wyłączone też, z tagiem. */
  readonly budgets: BudgetOption[];
}
