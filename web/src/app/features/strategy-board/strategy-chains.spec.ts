import { StrategyEdge, StrategyNode, StrategyNodeType } from '../../core/api/models/strategies';
import { buildChains } from './strategy-chains';

describe('buildChains', () => {
  const node = (id: string, type: StrategyNodeType, x = 0, y = 0): { node: StrategyNode } => ({
    node: {
      id, type, title: id, x, y, month: null, amount: null, rate: null, installment: null, mode: null, metric: null,
      comparison: null, threshold: null, categoryId: null, standingOrderId: null, actualMonth: null, actualAmount: null,
    },
  });
  const edge = (from: string, to: string, label: StrategyEdge['label'] = 'None'): StrategyEdge => ({ id: `${from}-${to}-${label}`, from, to, label });

  it('zdarzenie otwiera łańcuch, a kroki idą za strzałkami', () => {
    const views = [node('bonus', 'Income'), node('overpay', 'Overpay', 280), node('goal', 'SetSavingsGoal', 560)];

    const { chains, unlinked } = buildChains(views, [edge('bonus', 'overpay'), edge('overpay', 'goal')]);

    expect(chains).toHaveLength(1);
    expect(chains[0].root.node.id).toBe('bonus');
    expect(chains[0].steps.map((s) => [s.view.node.id, s.depth])).toEqual([['overpay', 0], ['goal', 0]]);
    expect(unlinked).toHaveLength(0);
  });

  it('gałęzie warunku idą w kolejności tak, nie i są wcięte, a pętla „Czekaj” nie dubluje kafelków', () => {
    const views = [
      node('bonus', 'Income'), node('check', 'Condition', 280), node('payoff', 'PayOffLoan', 560), node('wait', 'Wait', 560, 100),
    ];
    // „nie” dopisane PRZED „tak” — kolejność ma wynikać z etykiety, nie z kolejności strzałek
    const edges = [edge('bonus', 'check'), edge('check', 'wait', 'No'), edge('check', 'payoff', 'Yes'), edge('wait', 'check')];

    const { chains } = buildChains(views, edges);

    expect(chains[0].steps.map((s) => [s.view.node.id, s.label, s.depth])).toEqual([
      ['check', 'None', 0], ['payoff', 'Yes', 1], ['wait', 'No', 1],
    ]);
  });

  it('kafelek bez drogi od zdarzenia ląduje w „bez połączenia”, a łańcuchy idą od góry tablicy', () => {
    const views = [node('late', 'Income', 0, 200), node('early', 'Income', 0, 0), node('orphan', 'Overpay', 280, 50)];

    const { chains, unlinked } = buildChains(views, []);

    expect(chains.map((c) => c.root.node.id)).toEqual(['early', 'late']);
    expect(unlinked.map((v) => v.node.id)).toEqual(['orphan']);
  });
});
