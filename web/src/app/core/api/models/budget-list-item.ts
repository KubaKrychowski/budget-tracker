/**
 * Stan budżetu na liście w ustawieniach.
 *
 * Backend serializuje enum jako TEKST (camelCase), nie liczbę — `status === 1` w szablonie
 * nie mówiłoby nikomu, o który stan chodzi. Nazwy są kontraktem: zmiana po stronie API
 * wymaga zmiany tutaj.
 */
export type BudgetStatus = 'active' | 'disabled' | 'deleted';

/** Reguła transferu — tytuł zawiera frazę + zakres kwoty BEZWZGLĘDNEJ. */
export interface TitleAmountRule {
  titlePattern: string;
  amountFrom: number;
  amountTo: number;
}

/** Wiersz tabeli „Lista budżetów". */
export interface BudgetListItem {
  /** Publiczny BusinessId (Guid) — API nie wystawia kluczy z bazy. */
  id: string;
  name: string;
  /** Pierwszy dzień miesiąca, którego dotyczy budżet. */
  month: string;
  currency: string;
  initialBalance: number;
  /** `initialBalance` + suma transakcji budżetu. Liczone przez API, nigdy tutaj. */
  balance: number;
  /** Suma limitów per kategoria. Dziś zawsze 0 — pole budżetu rozstrzyga issue #5. */
  monthlyLimit: number;
  createdAt: string;
  transactionCount: number;
  status: BudgetStatus;
  disabledAt: string | null;
  deletedAt: string | null;
  /** Powiązany budżet oszczędnościowy (#10) — `null` = brak powiązania. */
  linkedSavingsBudgetId: string | null;
  /** Reguły rozpoznające własne transakcje jako transfer do/z powiązanego budżetu. */
  savingsTransferRules: TitleAmountRule[];
}

export interface BudgetListResponse {
  budgets: BudgetListItem[];
  /**
   * Ile dni usunięty budżet daje się przywrócić. Przychodzi z API, bo modal usuwania
   * obiecuje konkretną liczbę — trzymanie jej po stronie frontu zamieniłoby zmianę
   * ustawienia w cichą nieprawdę na ekranie.
   */
  retentionDays: number;
}
