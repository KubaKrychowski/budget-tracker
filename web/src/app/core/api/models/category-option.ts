import { CategoryType } from './managed-category';

/** Kategoria do wyboru przy korekcie wiersza w kroku 3. */
export interface CategoryOption {
  /** Publiczny BusinessId (Guid). */
  readonly id: string;
  readonly name: string;
  /** Wydatek albo wpływ — kategoria przychodowa nie przyjmie wydatku (API odpowie 400). */
  readonly type: CategoryType;
  /** Kategoria wspólna ukryta przez konto (#30) — API zwraca ją tylko przy `includeHidden=true`; selecty jej nie pokazują. */
  readonly hidden?: boolean;
}
