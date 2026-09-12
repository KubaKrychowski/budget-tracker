import { snapToAvailableRange } from './chart-range-selection';

describe('snapToAvailableRange', () => {
  const points = [
    { date: '2026-03-01' },
    { date: '2026-03-05' },
    { date: '2026-03-10' },
    { date: '2026-03-20' },
    { date: '2026-03-31' },
  ];
  const ms = (iso: string) => new Date(`${iso}T00:00:00`).getTime();

  it('returns null for an empty series', () => {
    expect(snapToAvailableRange([], ms('2026-03-01'), ms('2026-03-31'))).toBeNull();
  });

  it('snaps a mid-window drag to the nearest existing points on each side', () => {
    // Zaznaczenie gdzies w srodku 3/07..3/23 (nie trafia zadnego punktu wprost).
    const result = snapToAvailableRange(points, ms('2026-03-07'), ms('2026-03-23'));

    expect(result).not.toBeNull();
    expect(result![0].getDate()).toBe(10); // najblizszy punkt >= min
    expect(result![1].getDate()).toBe(20); // najblizszy punkt <= max
  });

  it('clamps to the first/last point when the drag overshoots the data', () => {
    const result = snapToAvailableRange(points, ms('2026-01-01'), ms('2026-12-31'));

    expect(result![0].getDate()).toBe(1);
    expect(result![0].getMonth()).toBe(2); // marzec
    expect(result![1].getDate()).toBe(31);
  });

  it('returns null when the selection is narrower than the gap between two points', () => {
    // Miedzy 3/10 a 3/20 nie ma zadnego punktu — zaznaczenie w tej luce nie daje sensownego zakresu.
    const result = snapToAvailableRange(points, ms('2026-03-12'), ms('2026-03-15'));

    expect(result).toBeNull();
  });

  it('picks distinct points, not the same point twice', () => {
    const result = snapToAvailableRange(points, ms('2026-03-01'), ms('2026-03-01'));

    // min i max identyczne trafiaja ten sam punkt po obu stronach -> from === to -> null.
    expect(result).toBeNull();
  });
});
