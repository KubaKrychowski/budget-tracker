/** Budżet do wyboru w kroku 1 importu. */
export interface ImportBudget {
  /** Publiczny BusinessId (Guid). */
  readonly id: string;
  readonly name: string;
  /** Pierwszy dzień miesiąca, którego dotyczy budżet (ISO). */
  readonly month: string;
}
