import { BudgetOption } from './budget-option';

/** Odpowiednik EpisodicOrderRowResponseDto — wiersz zaplanowanego albo zrealizowanego zlecenia epizodycznego. */
export interface EpisodicOrderRow {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  /** Kategoria planu — do edycji; przy zrealizowanym bez planu `null`. */
  readonly categoryId: string | null;
  /** Przy zrealizowanym — kategoria TRANSAKCJI, przy zaplanowanym — planu. */
  readonly categoryName: string | null;
  /** Dodatnia. Przy zrealizowanym — kwota transakcji. */
  readonly amount: number;
  /** Pierwszy dzień miesiąca terminu; `null` — bez terminu („przy okazji”) albo bez planu. */
  readonly dueMonth: string | null;
  /** Data transakcji; `null` przy zaplanowanym. */
  readonly date: string | null;
  readonly transactionId: string | null;
  readonly transactionDescription: string | null;
  /** Tylko takie zrealizowane da się cofnąć do zaplanowanych. */
  readonly wasPlanned: boolean;
  readonly reservationId: string | null;
  /** Uzbierane w rezerwacji — suma ręcznych wpłat, jak na ekranie rezerwacji. */
  readonly collected: number | null;
}

/** Odpowiednik EpisodicOrdersResponseDto — obie zakładki jednym żądaniem. */
export interface EpisodicOrdersResponse {
  readonly planned: EpisodicOrderRow[];
  readonly realized: EpisodicOrderRow[];
  readonly plannedTotal: number;
  readonly collectedTotal: number;
  readonly reservedTotal: number;
  readonly realizedThisYear: number;
  readonly withoutSavingsCount: number;
  readonly currentMonth: string;
  readonly selectedBudgetIds: string[];
  readonly budgets: BudgetOption[];
}

/** Odpowiednik EpisodicOrderCandidateResponseDto — wydatek do wskazania. */
export interface EpisodicOrderCandidate {
  readonly id: string;
  readonly date: string;
  readonly description: string;
  /** Ze znakiem, jak na wyciągu — wydatek jest ujemny. */
  readonly amount: number;
  readonly categoryName: string | null;
}
