/** Odpowiednik ProductCategoryResponseDto — kategoria produktu z paragonu (osobny słownik od kategorii budżetowych). */
export interface ProductCategory {
  readonly id: string;
  readonly name: string;
  /** Kategoria wspólna (bazowa) — tylko do odczytu. */
  readonly isShared: boolean;
}
