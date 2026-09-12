/** Budżet zwrócony po zapisie kroku 1 kreatora. */
export interface CreatedBudget {
  /** Publiczny BusinessId (Guid). */
  readonly id: string;
  readonly name: string;
  /** Pierwszy dzień miesiąca, którego dotyczy budżet (ISO). Ustala go serwer. */
  readonly month: string;
  readonly currency: string;
}
