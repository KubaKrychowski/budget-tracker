/**
 * Pamięć rozmiaru strony tabeli (liczby wierszy na stronę) między wizytami — zgłoszenie #31.
 *
 * @remarks
 * - ⚠️ Zapisujemy w `localStorage` pod prefiksem `bt.`: wylogowanie i zmiana konta czyszczą takie klucze
 *   (`App.clearLocalAppState`), więc kolejna osoba na tej przeglądarce nie dziedziczy cudzych ustawień. Cena: wybór jest per
 *   przeglądarka, nie per konto na serwerze — świadomie, jak „przeczytane” w „Co nowego”.
 * - Odczyt jest ODPORNY: brak wpisu, wartość spoza dozwolonych, uszkodzony wpis albo wyłączony storage (tryb prywatny)
 *   to po prostu „nic nie zapamiętano” — wygoda nigdy nie może wywrócić ekranu.
 * - Zapisana wartość jest tylko DOMYŚLNĄ: rozmiar podany jawnie w adresie (`?pageSize=20`, np. z odnośnika) ma pierwszeństwo
 *   i niczego nie nadpisuje — zapamiętuje się wyłącznie wybór zrobiony przez użytkownika w kontrolce tabeli.
 */
const PREFIX = 'bt.pageSize.';

/** Zapamiętany rozmiar strony tabeli `table`, jeśli jest jednym z `allowed`; inaczej `null`. */
export function rememberedPageSize(table: string, allowed: readonly number[]): number | null {
  try {
    const value = Number(localStorage.getItem(PREFIX + table));
    return Number.isInteger(value) && allowed.includes(value) ? value : null;
  } catch {
    return null;
  }
}

/** Zapamiętuje wybór użytkownika. Błąd zapisu (storage wyłączony) jest ignorowany — brak pamięci to najwyżej utracona wygoda. */
export function rememberPageSize(table: string, size: number): void {
  try {
    localStorage.setItem(PREFIX + table, String(size));
  } catch {
    // celowo pusto — patrz opis wyżej
  }
}
