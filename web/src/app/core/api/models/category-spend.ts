/** Rodzaj wiersza rozbicia: kategoria produktu, produkty bez kategorii albo część bez paragonu (odpowiednik ProductSpendKind). */
export type ProductSpendKind = 'Category' | 'Uncategorized' | 'NoReceipt';

/** Odpowiednik ProductSpendResponseDto — wiersz rozbicia kategorii na produkty z paragonów. */
export interface ProductSpend {
  /** Tylko dla `Category`. */
  readonly productCategoryId: string | null;
  /** Tylko dla `Category`; etykiety pozostałych rodzajów tłumaczy front po `kind`. */
  readonly name: string | null;
  readonly amount: number;
  readonly kind: ProductSpendKind;
}

export interface CategorySpend {
  /** `null` = kubełek „bez kategorii" — sentinel, nie brak danych. */
  categoryId: string | null;
  categoryName: string;
  amount: number;
  /** Rozbicie na produkty z paragonów; puste, gdy żadna transakcja kategorii nie ma paragonu z cenami. Suma wierszy = `amount`. */
  products: readonly ProductSpend[];
}
