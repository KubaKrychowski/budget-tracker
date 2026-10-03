import { StrategyNodeType } from '../../core/api/models/strategies';
import {
  canReceive, canSend, inputConnector, newNode, NODE_META, outputConnector, PALETTE, parseInputConnector,
  parseOutputConnector, unavailableReason,
} from './strategy-node-meta';

const ALL_TYPES = Object.keys(NODE_META) as StrategyNodeType[];

/** Rodzaje kafelków i identyfikatory złączy — reguły, na których stoi cała tablica. */
describe('strategy-node-meta', () => {
  it('każdy rodzaj kafelka ma dokładnie jedno miejsce w palecie', () => {
    const inPalette = Object.values(PALETTE).flat();

    expect([...inPalette].sort()).toEqual([...ALL_TYPES].sort());
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
    expect(unavailableReason('Income', 'SetLimit')).toBe('strategies.board.unavailable.incomeLimit');
    expect(unavailableReason('Expense', 'SetLimit')).toBeNull();
    expect(unavailableReason(null, 'SetLimit')).toBeNull();
    expect(unavailableReason('Income', 'Overpay')).toBeNull();
  });
});
