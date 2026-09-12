/** Komplet liczb z ekranu „Podsumowanie" (krok 4 makiety). */
export interface ImportSummary {
  readonly batchId: string;
  readonly rowsInFile: number;
  readonly imported: number;
  readonly pendingReview: number;
  readonly skippedDuplicates: number;
  readonly totalExpenses: number;
  readonly totalIncome: number;
  readonly periodFrom: string | null;
  readonly periodTo: string | null;
  readonly averageConfidence: number | null;
  readonly budgetBalance: number;
}
