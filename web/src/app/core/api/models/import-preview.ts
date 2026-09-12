import { PreviewRow } from './preview-row';

/** Wynik kroku „Podgląd". Niczego jeszcze nie zapisano. */
export interface ImportPreview {
  readonly rowsInFile: number;
  readonly willImport: number;
  readonly pendingReview: number;
  readonly skippedDuplicates: number;
  readonly rows: readonly PreviewRow[];
}
