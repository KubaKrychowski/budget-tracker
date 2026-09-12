/** Kierunek przepływu, na który reguła patrzy. Nazwy zgodne z enumem `RuleDirection` w API. */
export type RuleDirection = 'Any' | 'Expense' | 'Income';

/** Reguła kategoryzacji w postaci, w jakiej wychodzi z API. */
export interface CategoryRule {
  /** Publiczny BusinessId (Guid). */
  readonly id: string;
  readonly pattern: string | null;
  readonly transactionTypePattern: string | null;
  readonly direction: RuleDirection;
  readonly categoryId: string;
  readonly categoryName: string;
  /**
   * ⚠️ NIŻSZA liczba znaczy „sprawdzana wcześniej", a wygrywa pierwsza dopasowana reguła.
   * Dlatego lista reguł jest sortowana priorytetem rosnąco, a nie nazwą — patrz `RuleCategorizer`.
   */
  readonly priority: number;
  readonly minAmount: number | null;
  readonly maxAmount: number | null;
  readonly note: string | null;
}

/** Reguła wysyłana do API — ta sama postać przy tworzeniu, edycji i podglądzie trafień. */
export interface CategoryRuleRequest {
  readonly pattern: string | null;
  readonly transactionTypePattern: string | null;
  readonly direction: RuleDirection;
  readonly categoryId: string;
  readonly priority: number;
  readonly minAmount: number | null;
  readonly maxAmount: number | null;
  readonly note: string | null;
}
