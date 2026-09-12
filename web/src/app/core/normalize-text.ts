/**
 * Normalizacja tekstu pod wyszukiwanie: małe litery + usunięcie polskich ogonków,
 * żeby „łódź" i „lodz" trafiały na to samo dopasowanie. Wspólne dla wyszukiwarki
 * w nagłówku (`App`) i filtrów tekstowych w tabelach (np. „Nazwa" w ustawieniach).
 */
export function normalizeText(value: string): string {
  return value.toLocaleLowerCase('pl').normalize('NFD').replace(/\p{Diacritic}/gu, '');
}
