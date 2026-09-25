/** Odpowiednik SearchKind z BudgetTracker.Api.Features.Search.Consts — klucz grupy, nie jej nazwa. */
export type SearchKind =
  | 'categories'
  | 'budgets'
  | 'standingOrders'
  | 'episodicOrders'
  | 'transactions';

/** Odpowiednik SearchHitResponseDto — jedno trafienie jako DANE; wiersz składa ekran. */
export interface SearchHit {
  readonly id: string;
  readonly label: string;
  readonly categoryName: string | null;
  readonly date: string | null;
  /** Kwota ZE ZNAKIEM, tak jak w bazie — wydatek jest ujemny. */
  readonly amount: number | null;
  readonly budgetId: string | null;
  readonly budgetName: string | null;
}

/** Odpowiednik SearchGroupResponseDto. */
export interface SearchGroup {
  readonly kind: SearchKind;
  /** Ile jest wszystkich trafień tego rodzaju — nie ile ich przyszło w `hits`. */
  readonly total: number;
  readonly hits: SearchHit[];
}

/** Odpowiednik SearchResponseDto — cała odpowiedź wyszukiwarki. */
export interface SearchResponse {
  readonly query: string;
  readonly groups: SearchGroup[];
  readonly total: number;
  readonly budgetId: string | null;
  readonly budgetName: string | null;
  readonly allBudgets: boolean;
}
