export interface BudgetOption {
  /** Publiczny BusinessId (Guid) — API nie wystawia kluczy z bazy. */
  id: string;
  name: string;
  /** Pierwszy dzien miesiaca, ktorego dotyczy budzet. */
  month: string;
  /**
   * Budzet wylaczony zostaje na liscie i daje sie wybrac — historie oglada sie takze po
   * zamknieciu budzetu. Dashboard tylko go OZNACZA i gasi akcje dopisujace dane.
   */
  disabled: boolean;
}
