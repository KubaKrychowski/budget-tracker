/**
 * Pozycja słownika z API (`DictionaryResponseDto`) — sam kod, np. `PLN`.
 * Tekst dla użytkownika, jeśli jest potrzebny, składa front z i18n.
 */
export interface DictionaryEntry {
  readonly code: string;
}
