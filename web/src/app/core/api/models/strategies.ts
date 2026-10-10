import { BudgetOption } from './budget-option';

/** Odpowiednik StrategyNodeType z BudgetTracker.Api.Domain.Consts — rodzaj kafelka tablicy. */
export type StrategyNodeType =
  | 'Trigger' | 'Income' | 'Expense'
  | 'Surplus' | 'Loan' | 'CushionGoal'
  | 'IncreaseSurplus' | 'Overpay' | 'PayOffLoan' | 'SetSavingsGoal' | 'CreateReservation' | 'EndStandingOrder' | 'SetLimit'
  | 'CreateEpisodicOrder'
  | 'Condition' | 'Wait' | 'End';

/** Odpowiednik StrategyEdgeLabel — zwykłe połączenie albo wyjście warunku. */
export type StrategyEdgeLabel = 'None' | 'Yes' | 'No';

/** Odpowiednik StrategyConditionMetric — co porównuje warunek. */
export type StrategyConditionMetric = 'Cash' | 'Debt' | 'CashMinusDebt';

/** Odpowiednik StrategyConditionComparison — kierunek porównania. */
export type StrategyConditionComparison = 'AtLeast' | 'AtMost';

/** Odpowiednik OverpaymentMode — jak bank rozlicza nadpłatę. */
export type OverpaymentMode = 'ReduceInstallment' | 'ShortenPeriod';

/** Odpowiednik StrategyTemplate — od czego startuje nowa strategia. */
export type StrategyTemplate = 'Blank' | 'LoanAndCushion';

/** Odpowiednik StrategyProblemKind — rodzaj problemu pokazywany na węźle. */
export type StrategyProblemKind =
  | 'MissingParameter' | 'NoIncomingEdge' | 'EventWithoutChain' | 'WaitDoesNotReturn' | 'Cycle' | 'Duplicate';

/** Odpowiednik StrategyNodeRequestDto / StrategyNodeResponseDto — kafelek tablicy. */
export interface StrategyNode {
  readonly id: string;
  readonly type: StrategyNodeType;
  readonly title: string;
  readonly x: number;
  readonly y: number;
  /** Miesiąc zdarzenia (pierwszy dzień); dla kredytu miesiąc startu. */
  readonly month: string | null;
  readonly amount: number | null;
  /** Oprocentowanie roczne kredytu w procentach (13,29 = 13,29%). */
  readonly rate: number | null;
  readonly installment: number | null;
  readonly mode: OverpaymentMode | null;
  readonly metric: StrategyConditionMetric | null;
  readonly comparison: StrategyConditionComparison | null;
  /** Próg warunku; przy limicie kategorii — od ilu procent limitu ostrzegać. */
  readonly threshold: number | null;
  /** Kategoria (limit kategorii, wydatek jednorazowy). */
  readonly categoryId: string | null;
  /** Zlecenie stałe do zakończenia. */
  readonly standingOrderId: string | null;
  /** Miesiąc, w którym zdarzenie FAKTYCZNIE nastąpiło („zdarzenie nastąpiło”); `null` = liczy się plan (`month`). */
  readonly actualMonth: string | null;
  /** Faktyczna kwota wpływu albo wydatku — tylko razem z `actualMonth`. */
  readonly actualAmount: number | null;
}

/** Odpowiednik StrategyEdgeRequestDto / StrategyEdgeResponseDto — strzałka między kafelkami. */
export interface StrategyEdge {
  readonly id: string;
  readonly from: string;
  readonly to: string;
  readonly label: StrategyEdgeLabel;
}

/** Odpowiednik StrategyMonthResponseDto — stan na koniec miesiąca symulacji. */
export interface StrategyMonth {
  readonly month: string;
  readonly cash: number;
  readonly debt: number;
  readonly interest: number;
}

/** Odpowiednik StrategyNodeOutcomeResponseDto — co się stało z węzłem w symulacji. */
export interface StrategyNodeOutcome {
  readonly nodeId: string;
  readonly firedIn: string | null;
  readonly conditionMetIn: string | null;
}

/** Odpowiednik StrategyProblemResponseDto — problem na węźle. */
export interface StrategyProblem {
  readonly nodeId: string;
  readonly kind: StrategyProblemKind;
}

/** Odpowiednik StrategyResultResponseDto — wynik symulacji. */
export interface StrategyResult {
  readonly months: StrategyMonth[];
  readonly loanPaidOffIn: string | null;
  readonly cushionReachedIn: string | null;
  readonly finalCash: number;
  readonly totalInterest: number;
  readonly nodes: StrategyNodeOutcome[];
  readonly problems: StrategyProblem[];
}

/** Odpowiednik StrategyVariantRequestDto — wariant: te same kafelki, część z nich wyłączona. */
export interface StrategyVariant {
  readonly id: string;
  readonly name: string;
  /** Kafelki, które symulacja w tym wariancie pomija; „Bazowy” (bez wyłączeń) nie jest zapisywany. */
  readonly disabledNodeIds: readonly string[];
}

/** Odpowiednik StrategyVariantResponseDto — wariant zapisanej strategii z wynikiem symulacji. */
export interface StrategyVariantWithResult extends StrategyVariant {
  readonly result: StrategyResult;
}

/** Odpowiednik StrategyVariantResultResponseDto — wynik jednego wariantu niezapisanego grafu. */
export interface StrategyVariantResult {
  readonly id: string;
  readonly result: StrategyResult;
}

/** Odpowiednik StrategyVariantsResultResponseDto — wynik bazowy i wszystkich wariantów (`POST /api/strategies/simulate-variants`). */
export interface StrategyVariantsResult {
  readonly base: StrategyResult;
  readonly variants: StrategyVariantResult[];
}

/** Odpowiednik StrategyResponseDto — strategia z policzonym wynikiem. */
export interface Strategy {
  readonly id: string;
  readonly budgetId: string;
  readonly name: string;
  readonly startMonth: string;
  readonly startCash: number;
  readonly horizonMonths: number;
  readonly updatedAt: string;
  readonly nodes: StrategyNode[];
  readonly edges: StrategyEdge[];
  readonly result: StrategyResult;
  readonly variants: StrategyVariantWithResult[];
}

/** Odpowiednik SaveStrategyRequestDto — cała strategia do zapisu albo podglądu symulacji. */
export interface SaveStrategyRequest {
  readonly name: string;
  readonly startMonth: string;
  readonly startCash: number;
  readonly horizonMonths: number;
  readonly nodes: readonly StrategyNode[];
  readonly edges: readonly StrategyEdge[];
  readonly variants: readonly StrategyVariant[];
}

/** Odpowiednik DuplicateStrategyRequestDto — kopia strategii pod nową nazwą. */
export interface DuplicateStrategyRequest {
  readonly name: string;
  readonly copyVariants: boolean;
}

/** Odpowiednik CreateStrategyRequestDto. */
export interface CreateStrategyRequest {
  readonly budgetId: string | null;
  readonly name: string;
  readonly template: StrategyTemplate;
}

/** Odpowiednik StrategyListItemResponseDto — wiersz listy „Wybierz strategię”. */
export interface StrategyListItem {
  readonly id: string;
  readonly name: string;
  readonly eventCount: number;
  readonly actionCount: number;
  readonly updatedAt: string;
  /** Ile wariantów (poza bazowym) ma strategia. */
  readonly variantCount: number;
}

/** Odpowiednik StrategiesResponseDto — lista strategii jednego budżetu z przełącznikiem budżetów. */
export interface StrategiesResponse {
  readonly selectedBudgetId: string | null;
  readonly budgets: BudgetOption[];
  readonly strategies: StrategyListItem[];
}

/** Odpowiednik StrategyApplyStatus — co by się stało z akcją „do budżetu” przy zastosowaniu. */
export type StrategyApplyStatus = 'New' | 'Change' | 'Exists' | 'Waiting' | 'Incomplete';

/** Odpowiednik StrategyApplyItemResponseDto — akcja w oknie „Zastosuj w budżecie”. */
export interface StrategyApplyItem {
  readonly nodeId: string;
  readonly type: StrategyNodeType;
  readonly title: string;
  readonly amount: number | null;
  readonly status: StrategyApplyStatus;
  /** Miesiąc działania (limit, wydatek), ostatni miesiąc zlecenia albo termin rezerwacji. */
  readonly month: string | null;
  /** Kwota, którą akcja zastąpi (obecny cel albo limit). */
  readonly currentAmount: number | null;
}

/** Odpowiednik StrategyApplyPreviewResponseDto. */
export interface StrategyApplyPreview {
  readonly budgetId: string;
  readonly budgetName: string;
  readonly items: StrategyApplyItem[];
}

/** Odpowiednik ApplyStrategyRequestDto. */
export interface ApplyStrategyRequest {
  readonly nodeIds: readonly string[];
  /** Wariant, który stosujemy; brak = bazowy. */
  readonly variantId?: string | null;
}

/** Odpowiednik ApplyStrategyResponseDto. */
export interface ApplyStrategyResponse {
  readonly applied: string[];
  readonly skipped: string[];
}

/** Odpowiednik StrategyReferencesResponseDto — obiekty budżetu, na które wskazują kafelki. */
export interface StrategyReferences {
  readonly categories: { readonly id: string; readonly name: string }[];
  readonly standingOrders: { readonly id: string; readonly name: string; readonly expectedAmount: number }[];
}
