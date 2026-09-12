/**
 * Wartości robocze wiersza w trybie edycji inline — jedne i te same dla edycji
 * pojedynczej (menu wiersza, jeden `id`) i masowej (wiele naraz), bo oba wchodzą
 * przez ten sam mechanizm (patrz `Transactions.startEdit`).
 */
export interface DraftRow {
  date: Date;
  description: string;
  /**
   * `null` = pole wyczyszczone i jeszcze nie uzupełnione. NIE zastępujemy tego zerem:
   * użytkownik czyszczący kwotę, żeby wpisać nową, zamieniłby transakcję na 0,00 PLN
   * bez żadnego śladu. Ten stan blokuje zapis (patrz `Transactions.errorFor`).
   */
  amount: number | null;
  categoryId: string | null;
  isLargeExpense: boolean;
}
