/**
 * Dane jednego wywołania `ConfirmDialogService.confirm()`. Tytuł modala jest STAŁY
 * („Potwierdź operację") — tylko treść w środku jest dynamiczna, więc wołający podaje
 * dwa fragmenty tekstu zamiast całego widoku.
 */
export interface ConfirmDialogOptions {
  header: string;
  description?: string;
  confirmKey?: string;
  confirmText?: string;
  cancelText?: string;
  /**
   * Operacja niszcząca (usunięcie) — przycisk potwierdzenia na czerwono. Z `confirmKey` czerwony jest zawsze,
   * bo klucz do przepisania stawia się wyłącznie przy operacjach, których nie da się łatwo cofnąć.
   */
  danger?: boolean;
}
