/** Odpowiednik ReceiptReadResponseDto z BudgetTracker.Api.Features.Receipts.Contracts. */
export interface ReceiptRead {
  /** Publiczny BusinessId zapisanego paragonu. */
  readonly id: string;
  readonly fileName: string;
  readonly merchant: string | null;
  /** Data zakupu (RRRR-MM-DD). */
  readonly date: string | null;
  readonly total: number | null;
  /** Pole puste albo o niskiej pewności OCR — ekran oznacza je „sprawdź". */
  readonly merchantUncertain: boolean;
  readonly dateUncertain: boolean;
  readonly totalUncertain: boolean;
}

/** Odpowiednik ReceiptCandidateResponseDto — transakcja z importu, która może być tą z paragonu. */
export interface ReceiptCandidate {
  readonly id: string;
  readonly date: string;
  readonly description: string;
  readonly amount: number;
  /** `Exact` = ta sama kwota w oknie dat, `Possible` = kwota zbliżona. */
  readonly match: 'Exact' | 'Possible';
}

/** Odpowiednik ReceiptResponseDto — paragon przypięty do transakcji. */
export interface ReceiptItem {
  readonly id: string;
  readonly fileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly merchant: string | null;
  readonly date: string | null;
  readonly total: number | null;
}
