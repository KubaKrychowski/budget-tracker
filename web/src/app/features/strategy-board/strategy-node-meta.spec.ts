import { StrategyEdge, StrategyNodeType } from '../../core/api/models/strategies';
import {
  BUDGET_ACTIONS, CATEGORIES, canReceive, canSend, edgesAfterRetype, inputConnector, newNode, NODE_META, outputConnector,
  parseInputConnector, parseOutputConnector, retype, unavailableReason,
} from './strategy-node-meta';

const ALL_TYPES = Object.keys(NODE_META) as StrategyNodeType[];

/** Rodzaje kafelków i identyfikatory złączy — reguły, na których stoi cała tablica. */
describe('strategy-node-meta', () => {
  it('każdy rodzaj kafelka jest w dokładnie jednej grupie select-a swojej kategorii', () => {
    const grouped = Object.values(CATEGORIES).flatMap((c) => c.groups.flatMap((g) => g.types));

    expect([...grouped].sort()).toEqual([...ALL_TYPES].sort());
    for (const [category, { groups, default: first }] of Object.entries(CATEGORIES)) {
      expect(NODE_META[first].category).toBe(category);
      for (const type of groups.flatMap((g) => g.types)) expect(NODE_META[type].category).toBe(category);
    }
  });

  it('zdarzenia i węzły bazowe nie mają wejścia, a „Koniec” i węzły bazowe nie mają wyjścia', () => {
    for (const type of ['Trigger', 'Income', 'Expense', 'Surplus', 'Loan', 'CushionGoal'] as const) {
      expect(canReceive(type), `${type} nie przyjmuje strzałki`).toBe(false);
    }
    for (const type of ['Overpay', 'Condition', 'Wait', 'End'] as const) {
      expect(canReceive(type), `${type} przyjmuje strzałkę`).toBe(true);
    }

    expect(canSend('End')).toBe(false);
    expect(canSend('Surplus')).toBe(false);
    expect(canSend('Income')).toBe(true);
    expect(canSend('Condition')).toBe(true);
  });

  it('złącza wyjściowe i wejściowe dają się odczytać z identyfikatora z powrotem', () => {
    expect(parseOutputConnector(outputConnector('n1'))).toEqual({ nodeId: 'n1', label: 'None' });
    expect(parseOutputConnector(outputConnector('n1', 'Yes'))).toEqual({ nodeId: 'n1', label: 'Yes' });
    expect(parseOutputConnector(outputConnector('n1', 'No'))).toEqual({ nodeId: 'n1', label: 'No' });
    expect(parseInputConnector(inputConnector('n1'))).toBe('n1');
  });

  it('akcje „do budżetu” to dokładnie grupa „Zakłada w budżecie” i mają swoje pola', () => {
    const group = CATEGORIES.action.groups.find((g) => g.key === 'budget')!.types;

    expect([...BUDGET_ACTIONS].sort()).toEqual([...group].sort());
    expect(NODE_META.SetLimit.fields).toEqual(['category', 'amount', 'warning']);
    expect(NODE_META.EndStandingOrder.fields).toEqual(['standingOrder', 'month']);
    expect(NODE_META.CreateReservation.fields).toEqual(['amount', 'dueMonth']);
    expect(NODE_META.CreateEpisodicOrder.fields).toEqual(['category', 'amount']);
  });

  it('nowy limit dostaje domyślny próg ostrzeżenia, a rezerwacja nie dostaje terminu', () => {
    expect(newNode('SetLimit', 'a', 0, 0, '2026-10-01').threshold).toBe(80);
    expect(newNode('CreateReservation', 'a', 0, 0, '2026-10-01').month).toBeNull();
    expect(newNode('EndStandingOrder', 'a', 0, 0, '2026-10-01').month).toBe('2026-10-01');
  });

  it('wejście nie jest wyjściem i odwrotnie', () => {
    expect(parseOutputConnector(inputConnector('n1'))).toBeNull();
    expect(parseInputConnector(outputConnector('n1'))).toBeNull();
    expect(parseInputConnector('cokolwiek')).toBeNull();
  });

  it('identyfikator węzła z myślnikiem nie myli rozbioru złącza', () => {
    expect(parseOutputConnector(outputConnector('end-order', 'No'))).toEqual({ nodeId: 'end-order', label: 'No' });
  });

  it('nowy kafelek z miesiącem dostaje miesiąc startu strategii, a bez miesiąca — nie', () => {
    expect(newNode('Income', 'a', 0, 0, '2026-10-01').month).toBe('2026-10-01');
    expect(newNode('Loan', 'a', 0, 0, '2026-10-01').month).toBe('2026-10-01');
    expect(newNode('Overpay', 'a', 0, 0, '2026-10-01').month).toBeNull();
  });

  it('nowa nadpłata ma tryb „obniż ratę”, a nowy warunek — gotówka minus dług, co najmniej', () => {
    expect(newNode('Overpay', 'a', 0, 0, '2026-10-01').mode).toBe('ReduceInstallment');
    const condition = newNode('Condition', 'a', 0, 0, '2026-10-01');

    expect([condition.metric, condition.comparison, condition.threshold]).toEqual(['CashMinusDebt', 'AtLeast', null]);
  });

  it('po wpływie jednorazowym nie da się ustawić limitu kategorii, po innych zdarzeniach tak', () => {
    expect(unavailableReason(['Income'], 'SetLimit')).toBe('strategies.board.unavailable.incomeLimit');
    expect(unavailableReason(['Expense'], 'SetLimit')).toBeNull();
    expect(unavailableReason([], 'SetLimit')).toBeNull();
    expect(unavailableReason(['Income'], 'Overpay')).toBeNull();
  });

  describe('zmiana rodzaju kafelka', () => {
    const edge = (id: string, from: string, to: string, label: StrategyEdge['label'] = 'None'): StrategyEdge => ({ id, from, to, label });

    it('zachowuje kwotę, gdy nowy rodzaj też ją ma, i czyści resztę pól', () => {
      const overpay = { ...newNode('Overpay', 'a', 0, 0, '2026-10-01'), amount: 7000 };

      const surplus = retype(overpay, 'IncreaseSurplus', '2026-10-01');
      expect([surplus.type, surplus.amount, surplus.mode]).toEqual(['IncreaseSurplus', 7000, null]);

      const wait = retype(overpay, 'Wait', '2026-10-01');
      expect([wait.amount, wait.mode]).toEqual([null, null]);
    });

    it('nowy rodzaj dostaje swoje wartości domyślne: miesiąc, tryb nadpłaty, warunek', () => {
      const surplus = newNode('IncreaseSurplus', 'a', 0, 0, '2026-10-01');

      expect(retype(surplus, 'Overpay', '2026-10-01').mode).toBe('ReduceInstallment');
      const condition = retype(surplus, 'Condition', '2026-10-01');
      expect([condition.metric, condition.comparison]).toEqual(['CashMinusDebt', 'AtLeast']);
      expect(retype(newNode('Trigger', 'a', 0, 0, '2026-10-01'), 'Income', '2026-11-01').month).toBe('2026-10-01');
      expect(retype({ ...surplus, month: null }, 'Trigger', '2026-11-01').month).toBe('2026-11-01');
    });

    it('limit kategorii zachowuje kwotę i kategorię przy zmianie na wydatek jednorazowy, ale nie próg', () => {
      const limit = { ...newNode('SetLimit', 'a', 0, 0, '2026-10-01'), amount: 1500, threshold: 90, categoryId: 'c1' };

      const expense = retype(limit, 'CreateEpisodicOrder', '2026-10-01');
      expect([expense.amount, expense.categoryId, expense.threshold]).toEqual([1500, 'c1', null]);

      const back = retype(expense, 'SetLimit', '2026-10-01');
      expect([back.categoryId, back.threshold]).toEqual(['c1', 80]);
    });

    it('próg warunku nie przechodzi w próg ostrzeżenia limitu i odwrotnie', () => {
      const condition = { ...newNode('Condition', 'a', 0, 0, '2026-10-01'), threshold: 9000 };

      expect(retype(condition, 'SetLimit', '2026-10-01').threshold).toBe(80);
      expect(retype({ ...condition, type: 'SetLimit', threshold: 95 }, 'Condition', '2026-10-01').threshold).toBeNull();
    });

    it('zlecenie stałe i jego miesiąc przeżywają tylko w „Zakończ zlecenie stałe”', () => {
      const end = { ...newNode('EndStandingOrder', 'a', 0, 0, '2026-10-01'), standingOrderId: 'o1', month: '2027-07-01' };

      const reservation = retype(end, 'CreateReservation', '2026-10-01');
      expect([reservation.standingOrderId, reservation.month]).toEqual([null, '2027-07-01']);
      expect(retype(end, 'Overpay', '2026-10-01').month).toBeNull();
    });

    it('zdarzenie zmienione na stan wyjściowy traci wyjście, a przyjęte wejście zachowuje', () => {
      const edges = [edge('e1', 'n', 'x'), edge('e2', 'y', 'n')];

      expect(edgesAfterRetype(edges, 'n', 'Trigger', 'Surplus')).toEqual([]);
      expect(edgesAfterRetype([edge('e2', 'y', 'n')], 'n', 'IncreaseSurplus', 'Wait')).toHaveLength(1);
    });

    it('akcja zmieniona na zdarzenie traci strzałki wchodzące, a na „Koniec” — wychodzące', () => {
      const edges = [edge('e1', 'n', 'x'), edge('e2', 'y', 'n')];

      expect(edgesAfterRetype(edges, 'n', 'Overpay', 'Trigger').map((e) => e.id)).toEqual(['e1']);
      expect(edgesAfterRetype(edges, 'n', 'Overpay', 'End').map((e) => e.id)).toEqual(['e2']);
    });

    it('akcja zmieniona na warunek dostaje wyjścia tak/nie, a nadmiarowe odpadają', () => {
      const edges = [edge('e1', 'n', 'a'), edge('e2', 'n', 'b'), edge('e3', 'n', 'c')];

      const result = edgesAfterRetype(edges, 'n', 'Overpay', 'Condition');

      expect(result.map((e) => [e.id, e.label])).toEqual([['e1', 'Yes'], ['e2', 'No']]);
    });

    it('warunek zmieniony na akcję zamienia etykiety tak/nie na zwykłe strzałki', () => {
      const edges = [edge('e1', 'n', 'a', 'Yes'), edge('e2', 'n', 'b', 'No')];

      const result = edgesAfterRetype(edges, 'n', 'Condition', 'Overpay');

      expect(result.map((e) => e.label)).toEqual(['None', 'None']);
    });

    it('zmiana w obrębie tej samej roli nie rusza strzałek', () => {
      const edges = [edge('e1', 'n', 'a', 'Yes'), edge('e2', 'n', 'b', 'No'), edge('e3', 'y', 'n')];

      expect(edgesAfterRetype(edges, 'n', 'Condition', 'Condition')).toEqual(edges);
      expect(edgesAfterRetype(edges, 'n', 'Wait', 'Condition')).toEqual(edges);
    });
  });

  describe('fakty zdarzeń', () => {
    const income = { ...newNode('Income', 'i', 0, 0, '2026-10-01'), amount: 500, actualMonth: '2026-12-01', actualAmount: 650 };

    it('nowy kafelek nie ma faktu', () => {
      const created = newNode('Income', 'i', 0, 0, '2026-10-01');

      expect([created.actualMonth, created.actualAmount]).toEqual([null, null]);
    });

    it('zmiana wpływu na wydatek zostawia fakt, a na zdarzenie bez kwoty zostawia miesiąc, ale gubi kwotę faktu', () => {
      const expense = retype(income, 'Expense', '2026-10-01');
      const trigger = retype(income, 'Trigger', '2026-10-01');

      expect([expense.actualMonth, expense.actualAmount]).toEqual(['2026-12-01', 650]);
      expect([trigger.actualMonth, trigger.actualAmount]).toEqual(['2026-12-01', null]);
    });

    it('zmiana na akcję czyści fakt — akcja nie „następuje”, a zostawiony fakt serwer odrzuciłby (400)', () => {
      const action = retype(income, 'IncreaseSurplus', '2026-10-01');

      expect([action.actualMonth, action.actualAmount]).toEqual([null, null]);
    });
  });
});
