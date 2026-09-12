/** Jedno trafienie podglądu — tyle, ile trzeba, żeby użytkownik rozpoznał swoją transakcję. */
export interface RulePreviewMatch {
  readonly id: string;
  readonly date: string;
  readonly description: string;
  readonly transactionType: string;
  readonly amount: number;
  readonly currentCategoryName: string | null;
}

/**
 * Wynik podglądu reguły. Trzy liczby, nie jedna — każda odpowiada na inne pytanie.
 */
export interface RulePreview {
  /** Ile transakcji łapie sama ta reguła. */
  readonly matchCount: number;
  /**
   * ⚠️ Ile z tych trafień zabiera reguła stojąca WCZEŚNIEJ (niższy priorytet). Bez pokazania
   * tej liczby ekran obiecywałby skutek, którego reguła nie ma.
   */
  readonly shadowedCount: number;
  /** Na ilu transakcjach liczono — bez tego „0 trafień" nie mówi, czy wzorzec jest zły, czy brak danych. */
  readonly scannedCount: number;
  readonly samples: readonly RulePreviewMatch[];
}
