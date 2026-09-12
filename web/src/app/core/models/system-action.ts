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
  /** Czy pokazywac jako kafel „Szybkich akcji". */
  readonly quickAction: boolean;
}
