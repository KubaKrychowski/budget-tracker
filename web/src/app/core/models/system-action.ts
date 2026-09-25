/**
 * Pojedyncza akcja, ktora system potrafi wykonac.
 *
 * Na teraz zadna z tras docelowych nie istnieje — powstana w kolejnych zadaniach.
 * Do tego czasu `route` jest null i UI mowi o tym wprost, zamiast udawac nawigacje.
 */
export interface SystemAction {
  readonly key: string;
  /**
   * Klucz tlumaczenia (np. `actions.addTransaction`), NIE gotowy tekst.
   * Rozwiazywany przez TranslateService w miejscu uzycia.
   */
  readonly labelKey: string;
  /**
   * Nazwa ikony NG-ZORRO — uzywana tylko wtedy, gdy dla akcji nie ma `svg`.
   * Dodajac nowa, dopisz ja tez do `icons.ts`.
   */
  readonly icon: string;
  /** Sciezka do ikony z design systemu w `public/icons/`. Gdy jest, wygrywa z `icon`. */
  readonly svg?: string;
  readonly route: string | null;
  /**
   * Parametry adresu dolaczane do `route` — dzis wylacznie zakladki ustawien (`?tab=rules`).
   * Bez nich „Reguly kategoryzacji" ladowalyby na domyslnej zakladce budzetow.
   */
  readonly queryParams?: Record<string, string>;
  /** Czy pokazywac jako kafel „Szybkich akcji". */
  readonly quickAction: boolean;
  /** Sekcja katalogu „Wszystkie funkcje" — patrz `FUNCTION_GROUPS`. */
  readonly group: FunctionGroup;
}

/**
 * Sekcje katalogu funkcji. Podzial jest wedlug TEGO, PO CO sie wchodzi, a nie wedlug
 * tego, gdzie lezy kod — „Popraw kategorie" to codzienna robota przy transakcjach,
 * mimo ze kategoryzacja siedzi w ustawieniach.
 */
export type FunctionGroup = 'daily' | 'planning' | 'budgets' | 'settings';
