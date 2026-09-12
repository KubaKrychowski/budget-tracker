export interface CategorySpend {
  /** `null` = kubełek „bez kategorii" — sentinel, nie brak danych. */
  categoryId: string | null;
  categoryName: string;
  amount: number;
}
