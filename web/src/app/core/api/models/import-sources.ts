import { ImportBudget } from './import-budget';
import { CategoryOption } from './category-option';

/**
 * Wszystko, czego stepper potrzebuje z góry, jednym żądaniem.
 *
 * `banks` to KLUCZE parserów zarejestrowanych po stronie API (np. `pko`), nie gotowe
 * nazwy — dołożenie banku nie wymaga zmiany w kodzie frontu, wystarczy dopisać nazwę
 * do `banks.*` w `public/i18n/*.json`.
 */
export interface ImportSources {
  readonly banks: readonly string[];
  readonly budgets: readonly ImportBudget[];
  readonly categories: readonly CategoryOption[];
}
