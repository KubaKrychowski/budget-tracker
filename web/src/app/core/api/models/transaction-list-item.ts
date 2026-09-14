/** Odpowiednik TransactionListItemResponseDto z BudgetTracker.Api.Features.Transactions.Contracts. */
export interface TransactionListItem {
  /** Publiczny BusinessId (Guid). */
  id: string;
  date: string;
  description: string;
  amount: number;
  categoryId: string | null;
  categoryName: string | null;
  /** Nazwa enuma `TransactionStatus` (np. „PendingReview") — tłumacz przez `enumTranslate`. */
  status: string;
  /** Zlecenie epizodyczne zrealizowane tą transakcją — kolumna „Epizodyczne” (dawniej flaga „duży wydatek”). */
  episodicOrderId: string | null;
  episodicOrderName: string | null;
  confidence: number | null;
}
