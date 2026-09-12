/** Odpowiednik TrainingReportResponseDto z BudgetTracker.Api.Features.Categorization.Contracts. */
export interface TrainingReport {
  rows: number;
  categories: number;
  /** Trafność per przykład — dominują ją kategorie liczne. */
  microAccuracy: number;
  /** Średnia trafność per kategoria — uczciwiej pokazuje, jak model radzi sobie z rzadkimi. */
  macroAccuracy: number;
}

/** Skład zbioru: ile z pliku bazowego, ile z poprawek użytkownika. */
export interface TrainingSetComposition {
  fromFile: number;
  fromCorrections: number;
  /**
   * Wiersze pliku bazowego, którym poprawka ZMIENIŁA etykietę. Zbiór od tego nie rośnie,
   * ale model uczy się z nich czegoś innego niż wcześniej — to właściwy dowód, że pętla działa.
   */
  corrected: number;
  /** Poprawki odsiane jako już obecne w pliku Z TĄ SAMĄ kategorią — bez tej liczby suma wygląda na błąd. */
  duplicates: number;
  total: number;
}

export interface CategoryExampleCount {
  name: string;
  /** `0` znaczy „model nigdy tej kategorii nie wskaże", nie „brak danych". */
  count: number;
}

export interface ModelVersion {
  /** Znacznik czasu `yyyyMMddHHmmss` — jednocześnie identyfikator do przywrócenia. */
  version: string;
  createdAt: string;
  isActive: boolean;
  /** `null` dla modelu sprzed wprowadzenia historii. */
  report: TrainingReport | null;
}

/** Odpowiednik TrainingSetOverview — wszystko, czego potrzebuje zakładka „Dane treningowe". */
export interface TrainingSetOverview {
  composition: TrainingSetComposition;
  categories: CategoryExampleCount[];
  pendingReview: number;
  correctionsSinceLastTraining: number;
  models: ModelVersion[];
  /** Zawsze `true` — patrz komentarz przy kontrakcie w API. */
  metricsAreIndicative: boolean;
}

/**
 * Wynik przeliczenia kategorii wierszy, które są już w bazie.
 *
 * Liczby są tu dlatego, że operacja zmienia dane, których użytkownik w tym momencie nie widzi —
 * „gotowe" bez nich byłoby prośbą o zaufanie.
 */
export interface RecategorizeReport {
  /** Ile wierszy wzięto pod uwagę — bez tych, o których zdecydował człowiek. */
  examined: number;
  recategorized: number;
  /** Ile straciło kategorię i wróciło do kolejki. Jedyna zmiana, która dokłada pracy. */
  movedToReview: number;
  unchanged: number;
}
