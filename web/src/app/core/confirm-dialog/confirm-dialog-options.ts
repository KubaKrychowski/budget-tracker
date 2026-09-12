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
}
