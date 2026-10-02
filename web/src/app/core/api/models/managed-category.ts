/** Typ kategorii — nazwy zgodne z enumem `CategoryType` w API. */
export type CategoryType = 'Expense' | 'Income';

/** Kategoria na liście zarządzania (`GET /api/categories/manage`) — 1:1 z `CategoryResponseDto`. */
export interface ManagedCategory {
  /** Publiczny BusinessId (Guid). */
  readonly id: string;
  readonly name: string;
  readonly type: CategoryType;
  /** Kategoria wspólna (bazowa) — tylko do odczytu. */
  readonly isShared: boolean;
  /** Transakcje zalogowanego konta w tej kategorii. */
  readonly transactions: number;
  /** Reguły kategoryzacji (własne i wspólne) wskazujące na tę kategorię. */
  readonly rules: number;
  /** Limity budżetowe konta na tej kategorii. */
  readonly limits: number;
  /** Używana przez transakcje, reguły albo limity — nie da się wtedy zmienić typu. */
  readonly inUse: boolean;
}

/** Ciało `POST /api/categories` i `PUT /api/categories/{id}`. */
export interface CategoryRequest {
  readonly name: string;
  readonly type: CategoryType;
}
