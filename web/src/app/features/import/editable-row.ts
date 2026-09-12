import { PreviewRow } from '../../core/api/models/preview-row';

/**
 * Wiersz podglądu w wersji roboczej — po stronie klienta, przed zapisem.
 *
 * Krok 3 makiety pozwala usuwać wiersze i poprawiać im kategorie, więc tabela pracuje
 * na kopii, a nie na odpowiedzi serwera.
 */
export interface EditableRow extends Omit<PreviewRow, 'categoryId' | 'categoryName'> {
  /** Publiczny BusinessId kategorii (Guid), nie klucz z bazy. */
  categoryId: string | null;
  categoryName: string | null;

  /**
   * Czy kategorię wskazał człowiek. Jedzie do API, bo rozstrzyga status po zapisie:
   * poprawka użytkownika nie może wrócić do kolejki „do przeglądu".
   */
  edited: boolean;
}
