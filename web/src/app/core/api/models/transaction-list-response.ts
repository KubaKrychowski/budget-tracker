import { BudgetOption } from './budget-option';
import { TransactionListItem } from './transaction-list-item';

/** Odpowiednik TransactionSummaryResponseDto z BudgetTracker.Api.Features.Transactions.Contracts. */
export interface TransactionSummary {
  /** Jako wartość dodatnia — tak, jak prezentuje ją UI. */
  totalExpenses: number;
  totalIncome: number;
  /** Saldo ze znakiem = `totalIncome - totalExpenses`. Bez własnego kafla — licznik pod tabelą. */
  balance: number;
  largestExpenseAmount: number;
}

/** Odpowiednik TransactionListResponseDto z BudgetTracker.Api.Features.Transactions.Contracts. */
export interface TransactionListResponse {
  items: TransactionListItem[];
  total: number;
  page: number;
  pageSize: number;
  /**
   * Agregaty kafli „Podsumowanie" dla CAŁEGO pasującego zestawu — po filtrach, ale
   * z pominięciem stronicowania.
   */
  summary: TransactionSummary;
  /**
   * Budżety, na których serwer FAKTYCZNIE policzył odpowiedź — wybrane przez użytkownika albo
   * jeden domyślny. Pusta lista tylko wtedy, gdy w bazie nie ma ani jednego budżetu.
   */
  selectedBudgetIds: string[];

  /** Pozycje multiselecta nad tabelą — jadą razem z listą, bez osobnego żądania. */
  budgets: BudgetOption[];

  /** Czy WYBRANE budżety mają jakiekolwiek transakcje, bez względu na filtr. */
  hasAnyTransactions: boolean;

  /** Nazwa zlecenia stałego z filtra `standingOrderId` — do etykiety „Zlecenie stałe: …”. */
  standingOrderName?: string | null;
}
