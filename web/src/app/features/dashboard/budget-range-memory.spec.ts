import { loadRememberedRange, rememberRange } from './budget-range-memory';

describe('budget-range-memory', () => {
  beforeEach(() => localStorage.clear());

  it('returns null when nothing was remembered for this budget', () => {
    expect(loadRememberedRange('11111111-1111-1111-1111-111111111111')).toBeNull();
  });

  it('round-trips the exact days, not shifted by timezone conversion', () => {
    const budgetId = '22222222-2222-2222-2222-222222222222';
    const from = new Date(2026, 0, 1); // 1 stycznia — dzień, na którym łatwo złapać przesunięcie UTC
    const to = new Date(2026, 0, 31);

    rememberRange(budgetId, [from, to]);
    const restored = loadRememberedRange(budgetId);

    expect(restored).not.toBeNull();
    expect(restored![0].getFullYear()).toBe(2026);
    expect(restored![0].getMonth()).toBe(0);
    expect(restored![0].getDate()).toBe(1);
    expect(restored![1].getDate()).toBe(31);
  });

  it('keeps ranges of different budgets apart', () => {
    const a = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const b = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

    rememberRange(a, [new Date(2026, 2, 1), new Date(2026, 2, 31)]);
    rememberRange(b, [new Date(2026, 5, 1), new Date(2026, 5, 30)]);

    expect(loadRememberedRange(a)![0].getMonth()).toBe(2);
    expect(loadRememberedRange(b)![0].getMonth()).toBe(5);
  });

  it('ignores corrupted storage instead of throwing', () => {
    const budgetId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
    localStorage.setItem(`budget-tracker:dashboard-range:${budgetId}`, '{not json');

    expect(loadRememberedRange(budgetId)).toBeNull();
  });
});
