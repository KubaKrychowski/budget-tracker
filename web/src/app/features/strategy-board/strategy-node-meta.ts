import { StrategyEdge, StrategyEdgeLabel, StrategyNode, StrategyNodeType } from '../../core/api/models/strategies';

/** Rola kafelka na tablicy — decyduje o wyglądzie, wejściu i wyjściach. */
export type NodeKind = 'event' | 'base' | 'action' | 'condition' | 'wait' | 'end';

/** Kategoria kafelka — trzy pozycje palety „Dodaj kafelek”; rodzaj w obrębie kategorii wybiera się w ustawieniach kafelka. */
export type NodeCategory = 'event' | 'action' | 'control';

/** Pola formularza węzła — rodzaj kafelka wybiera, które się pokazują. */
export type NodeField = 'month' | 'amount' | 'rate' | 'installment' | 'mode' | 'condition';

export interface NodeMeta {
  readonly type: StrategyNodeType;
  readonly kind: NodeKind;
  readonly category: NodeCategory;
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
  Trigger: { type: 'Trigger', kind: 'event', category: 'event', fields: ['month'] },
  Income: { type: 'Income', kind: 'event', category: 'event', fields: ['month', 'amount'] },
  Expense: { type: 'Expense', kind: 'event', category: 'event', fields: ['month', 'amount'] },
  Surplus: { type: 'Surplus', kind: 'base', category: 'event', fields: ['amount'] },
  Loan: { type: 'Loan', kind: 'base', category: 'event', fields: ['month', 'amount', 'rate', 'installment'] },
  CushionGoal: { type: 'CushionGoal', kind: 'base', category: 'event', fields: ['amount'] },
  IncreaseSurplus: { type: 'IncreaseSurplus', kind: 'action', category: 'action', fields: ['amount'] },
  Overpay: { type: 'Overpay', kind: 'action', category: 'action', fields: ['amount', 'mode'] },
  PayOffLoan: { type: 'PayOffLoan', kind: 'action', category: 'action', fields: [] },
  SetSavingsGoal: { type: 'SetSavingsGoal', kind: 'action', category: 'action', fields: ['amount'] },
  CreateReservation: { type: 'CreateReservation', kind: 'action', category: 'action', fields: ['amount'] },
  EndStandingOrder: { type: 'EndStandingOrder', kind: 'action', category: 'action', fields: [] },
  SetLimit: { type: 'SetLimit', kind: 'action', category: 'action', fields: ['amount'] },
  Condition: { type: 'Condition', kind: 'condition', category: 'control', fields: ['condition'] },
  Wait: { type: 'Wait', kind: 'wait', category: 'control', fields: [] },
  End: { type: 'End', kind: 'end', category: 'control', fields: [] },
};

/** Grupa rodzajów w liście „Rodzaj” — klucz tłumaczenia to `strategies.board.groups.<key>`. */
export interface TypeGroup {
  readonly key: string;
  readonly types: readonly StrategyNodeType[];
}

/**
 * Kategorie kafelków: rodzaj domyślny nowego kafelka i grupy rodzajów do wyboru w ustawieniach (makieta Figma
 * „Strategia”: 388:361, 388:626, 388:896). Grupa „budget” akcji to te, które po „Zastosuj w budżecie” zakładają
 * w aplikacji prawdziwe obiekty; „simulated” liczy tylko symulacja.
 */
export const CATEGORIES: Readonly<Record<NodeCategory, { readonly default: StrategyNodeType; readonly groups: readonly TypeGroup[] }>> = {
  event: {
    default: 'Trigger',
    groups: [
      { key: 'events', types: ['Trigger', 'Income', 'Expense'] },
      { key: 'base', types: ['Surplus', 'Loan', 'CushionGoal'] },
    ],
  },
  action: {
    default: 'IncreaseSurplus',
    groups: [
      { key: 'simulated', types: ['IncreaseSurplus', 'Overpay', 'PayOffLoan'] },
      { key: 'budget', types: ['SetSavingsGoal', 'CreateReservation', 'SetLimit', 'EndStandingOrder'] },
    ],
  },
  control: {
    default: 'Condition',
    groups: [{ key: 'flow', types: ['Condition', 'Wait', 'End'] }],
  },
};

export const CATEGORY_ORDER: readonly NodeCategory[] = ['event', 'action', 'control'];

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
 * Powód, dla którego rodzaj nie pasuje do kafelka, z którego do niego prowadzi strzałka — klucz zasobu albo `null`,
 * gdy pasuje. Jedyna reguła pierwszej wersji: wpływ jednorazowy nie zmienia wydatków, więc nie ma sensu ustawiać po nim
 * limitu kategorii (makieta: „premia nie zmienia wydatków”). Tabela zgodności rośnie razem z rodzajami zdarzeń.
 */
export const unavailableReason = (incoming: readonly StrategyNodeType[], candidate: StrategyNodeType): string | null =>
  candidate === 'SetLimit' && incoming.includes('Income') ? 'strategies.board.unavailable.incomeLimit' : null;

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

/**
 * Kafelek po zmianie rodzaju: zostają pola, które nowy rodzaj też ma (np. kwota przy zmianie „Zwiększ nadwyżkę” na
 * „Nadpłać kredyt”), reszta jest czyszczona — nie zostaje po niej nic, co mogłoby wpłynąć na symulację po cichu.
 */
export const retype = (node: StrategyNode, type: StrategyNodeType, startMonth: string): StrategyNode => {
  const fields = NODE_META[type].fields;
  const keep = <T>(field: NodeField, value: T): T | null => (fields.includes(field) ? value : null);
  const isCondition = type === 'Condition';
  return {
    ...node,
    type,
    month: fields.includes('month') ? (node.month ?? startMonth) : null,
    amount: keep('amount', node.amount),
    rate: keep('rate', node.rate),
    installment: keep('installment', node.installment),
    mode: type === 'Overpay' ? (node.mode ?? 'ReduceInstallment') : null,
    metric: isCondition ? (node.metric ?? 'CashMinusDebt') : null,
    comparison: isCondition ? (node.comparison ?? 'AtLeast') : null,
    threshold: keep('condition', node.threshold),
  };
};

/**
 * Strzałki po zmianie rodzaju kafelka `id` z `from` na `to`: znikają te, których nowy rodzaj nie może mieć
 * (wejście do zdarzenia, wyjście z „Koniec”), a wyjścia przy zmianie z/na warunek dostają właściwe etykiety
 * (warunek: tak i nie, reszta: zwykła strzałka) — nadmiarowe wyjścia warunku odpadają.
 */
export const edgesAfterRetype = (
  edges: readonly StrategyEdge[], id: string, from: StrategyNodeType, to: StrategyNodeType,
): StrategyEdge[] => {
  let outgoing = 0;
  const result: StrategyEdge[] = [];
  for (const edge of edges) {
    if (edge.to === id && !canReceive(to)) continue;
    if (edge.from !== id) {
      result.push(edge);
      continue;
    }
    if (!canSend(to)) continue;
    if ((from === 'Condition') === (to === 'Condition')) {
      result.push(edge);
      continue;
    }
    if (to === 'Condition') {
      const label: StrategyEdgeLabel | null = outgoing === 0 ? 'Yes' : outgoing === 1 ? 'No' : null;
      outgoing++;
      if (label) result.push({ ...edge, label });
    } else {
      result.push({ ...edge, label: 'None' });
    }
  }
  return result;
};
