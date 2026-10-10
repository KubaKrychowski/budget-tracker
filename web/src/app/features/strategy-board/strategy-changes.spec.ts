import { SaveStrategyRequest, StrategyEdge, StrategyNode } from '../../core/api/models/strategies';
import { describeChanges, summarizeChanges } from './strategy-changes';

const node = (over: Partial<StrategyNode>): StrategyNode => ({
  id: 'x', type: 'Trigger', title: '', x: 0, y: 0, month: null, amount: null, rate: null, installment: null,
  mode: null, metric: null, comparison: null, threshold: null, categoryId: null, standingOrderId: null, ...over,
});

const edge = (id: string, from: string, to: string): StrategyEdge => ({ id, from, to, label: 'None' });

const base: SaveStrategyRequest = {
  name: 'Plan', startMonth: '2026-10-01', startCash: 1000, horizonMonths: 24,
  nodes: [node({ id: 'a', x: 10, y: 10 }), node({ id: 'b', type: 'Overpay', amount: 500 })],
  edges: [edge('e1', 'a', 'b')],
  variants: [],
};

describe('strategy-changes', () => {
  it('bez zmian nie ma niczego do wypisania', () => {
    expect(describeChanges(summarizeChanges(base, { ...base }))).toEqual([]);
  });

  it('przesunięcie albo zmiana kwoty to zmieniony kafelek, a nowy i usunięty liczą się osobno', () => {
    const working: SaveStrategyRequest = {
      ...base,
      nodes: [node({ id: 'a', x: 99, y: 10 }), node({ id: 'c', type: 'End' })],
    };

    const summary = summarizeChanges(base, working);

    expect(summary).toMatchObject({ nodesAdded: 1, nodesRemoved: 1, nodesChanged: 1 });
  });

  it('ta sama strzałka pod nowym identyfikatorem nie jest zmianą, a inny cel jest dodaniem i usunięciem', () => {
    const renamed = summarizeChanges(base, { ...base, edges: [edge('inny-id', 'a', 'b')] });
    const rewired = summarizeChanges(base, { ...base, edges: [edge('e1', 'b', 'a')] });

    expect(renamed).toMatchObject({ edgesAdded: 0, edgesRemoved: 0 });
    expect(rewired).toMatchObject({ edgesAdded: 1, edgesRemoved: 1 });
  });

  it('wykrywa zmiany wariantów i parametrów strategii, ale nie zwykłą spację w nazwie', () => {
    const variants = summarizeChanges(base, { ...base, variants: [{ id: 'v1', name: 'Bez a', disabledNodeIds: ['a'] }] });
    const params = summarizeChanges(base, { ...base, horizonMonths: 36 });
    const spaces = summarizeChanges(base, { ...base, name: '  Plan ' });

    expect(variants.variantsChanged).toBe(true);
    expect(params.paramsChanged).toBe(true);
    expect(spaces.paramsChanged).toBe(false);
  });

  it('describeChanges wypisuje tylko niezerowe pozycje w stałej kolejności', () => {
    const lines = describeChanges({
      nodesAdded: 2, nodesRemoved: 0, nodesChanged: 3, edgesAdded: 0, edgesRemoved: 1, variantsChanged: true, paramsChanged: false,
    });

    expect(lines).toEqual([
      { key: 'nodesAdded', count: 2 },
      { key: 'nodesChanged', count: 3 },
      { key: 'edgesRemoved', count: 1 },
      { key: 'variantsChanged', count: 1 },
    ]);
  });
});
