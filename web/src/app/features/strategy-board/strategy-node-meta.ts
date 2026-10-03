import { StrategyEdgeLabel, StrategyNode, StrategyNodeType } from '../../core/api/models/strategies';

/** Rola kafelka na tablicy — decyduje o wyglądzie, wejściu i wyjściach. */
export type NodeKind = 'event' | 'base' | 'action' | 'condition' | 'wait' | 'end';

/** Zakładka palety „Dodaj kafelek”. */
export type PaletteTab = 'events' | 'actions' | 'controls';

/** Pola formularza węzła — rodzaj kafelka wybiera, które się pokazują. */
export type NodeField = 'month' | 'amount' | 'rate' | 'installment' | 'mode' | 'condition';

export interface NodeMeta {
  readonly type: StrategyNodeType;
  readonly kind: NodeKind;
  readonly tab: PaletteTab;
  readonly fields: readonly NodeField[];
}

/**
 * Rodzaje kafelków i ich zachowanie na tablicy — odpowiednik `StrategyNodeType` z API.
 *
 * ⚠️ Zdarzenia (`event`) i węzły bazowe (`base`) NIE mają wejścia: nic do nich nie prowadzi, bo to początki łańcuchów
 * albo stan wyjściowy. Reszta ma jedno wejście. Warunek ma dwa wyjścia (tak i nie), pozostałe jedno; „Koniec” nie ma
 * wyjścia. Te same reguły egzekwuje analizator grafu po stronie API (`StrategyGraphAnalyzer`).
 */
export const NODE_META: Readonly<Record<StrategyNodeType, NodeMeta>> = {
  Trigger: { type: 'Trigger', kind: 'event', tab: 'events', fields: ['month'] },
  Income: { type: 'Income', kind: 'event', tab: 'events', fields: ['month', 'amount'] },
  Expense: { type: 'Expense', kind: 'event', tab: 'events', fields: ['month', 'amount'] },
  Surplus: { type: 'Surplus', kind: 'base', tab: 'events', fields: ['amount'] },
  Loan: { type: 'Loan', kind: 'base', tab: 'events', fields: ['month', 'amount', 'rate', 'installment'] },
  CushionGoal: { type: 'CushionGoal', kind: 'base', tab: 'events', fields: ['amount'] },
  IncreaseSurplus: { type: 'IncreaseSurplus', kind: 'action', tab: 'actions', fields: ['amount'] },
  Overpay: { type: 'Overpay', kind: 'action', tab: 'actions', fields: ['amount', 'mode'] },
  PayOffLoan: { type: 'PayOffLoan', kind: 'action', tab: 'actions', fields: [] },
  SetSavingsGoal: { type: 'SetSavingsGoal', kind: 'action', tab: 'actions', fields: ['amount'] },
  CreateReservation: { type: 'CreateReservation', kind: 'action', tab: 'actions', fields: ['amount'] },
  EndStandingOrder: { type: 'EndStandingOrder', kind: 'action', tab: 'actions', fields: [] },
  SetLimit: { type: 'SetLimit', kind: 'action', tab: 'actions', fields: ['amount'] },
  Condition: { type: 'Condition', kind: 'condition', tab: 'controls', fields: ['condition'] },
  Wait: { type: 'Wait', kind: 'wait', tab: 'controls', fields: [] },
  End: { type: 'End', kind: 'end', tab: 'controls', fields: [] },
};

/** Kafelki w kolejności, w jakiej pokazuje je paleta, pogrupowane po zakładkach. */
export const PALETTE: Readonly<Record<PaletteTab, readonly StrategyNodeType[]>> = {
  events: ['Trigger', 'Income', 'Expense', 'Surplus', 'Loan', 'CushionGoal'],
  actions: ['IncreaseSurplus', 'Overpay', 'PayOffLoan', 'SetSavingsGoal', 'CreateReservation', 'EndStandingOrder', 'SetLimit'],
  controls: ['Condition', 'Wait', 'End'],
};

export const PALETTE_TABS: readonly PaletteTab[] = ['events', 'actions', 'controls'];

/** Czy do kafelka może prowadzić strzałka. */
export const canReceive = (type: StrategyNodeType): boolean => {
  const kind = NODE_META[type].kind;
  return kind !== 'event' && kind !== 'base';
};

/** Czy z kafelka może wychodzić strzałka — poza „Koniec” i węzłami bazowymi, które niczego nie uruchamiają. */
export const canSend = (type: StrategyNodeType): boolean => {
  const kind = NODE_META[type].kind;
  return kind !== 'end' && kind !== 'base';
};

/**
 * Powód, dla którego kafelek nie pasuje do zaznaczonego zdarzenia — klucz zasobu albo `null`, gdy pasuje.
 *
 * Jedyna reguła pierwszej wersji: wpływ jednorazowy nie zmienia wydatków, więc nie ma sensu ustawiać po nim limitu
 * kategorii (makieta: „premia nie zmienia wydatków”). Tabela zgodności rośnie razem z rodzajami zdarzeń.
 */
export const unavailableReason = (selected: StrategyNodeType | null, candidate: StrategyNodeType): string | null =>
  selected === 'Income' && candidate === 'SetLimit' ? 'strategies.board.unavailable.incomeLimit' : null;

/** Identyfikator złącza wejściowego węzła. */
export const inputConnector = (nodeId: string): string => `${nodeId}-in`;

/** Identyfikator złącza wyjściowego: warunek ma dwa (`-out-yes`, `-out-no`), reszta jedno (`-out`). */
export const outputConnector = (nodeId: string, label: StrategyEdgeLabel = 'None'): string =>
  label === 'Yes' ? `${nodeId}-out-yes` : label === 'No' ? `${nodeId}-out-no` : `${nodeId}-out`;

/** Rozbiera identyfikator złącza wyjściowego na węzeł i etykietę; `null`, gdy to nie jest złącze wyjściowe. */
export const parseOutputConnector = (connectorId: string): { nodeId: string; label: StrategyEdgeLabel } | null => {
  for (const [suffix, label] of [['-out-yes', 'Yes'], ['-out-no', 'No'], ['-out', 'None']] as const) {
    if (connectorId.endsWith(suffix)) return { nodeId: connectorId.slice(0, -suffix.length), label };
  }
  return null;
};

/** Rozbiera identyfikator złącza wejściowego na węzeł; `null`, gdy to nie jest złącze wejściowe. */
export const parseInputConnector = (connectorId: string): string | null =>
  connectorId.endsWith('-in') ? connectorId.slice(0, -'-in'.length) : null;

/** Nowy kafelek z rozsądnymi wartościami domyślnymi — brakujące parametry zostają puste i widać je jako problem. */
export const newNode = (type: StrategyNodeType, id: string, x: number, y: number, startMonth: string): StrategyNode => {
  const fields = NODE_META[type].fields;
  return {
    id,
    type,
    title: '',
    x,
    y,
    month: fields.includes('month') ? startMonth : null,
    amount: null,
    rate: null,
    installment: null,
    mode: type === 'Overpay' ? 'ReduceInstallment' : null,
    metric: type === 'Condition' ? 'CashMinusDebt' : null,
    comparison: type === 'Condition' ? 'AtLeast' : null,
    threshold: null,
  };
};
