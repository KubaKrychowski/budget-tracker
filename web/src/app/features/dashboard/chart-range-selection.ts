import { fromIsoDate } from '../../core/api/date-param';

/**
 * Zaznaczenie myszą (drag-zoom na wykresie apexcharts) rzadko trafia DOKŁADNIE w punkt
 * danych — `chart.events.zoomed` zwraca timestampy gdzieś W ŚRODKU przeciąganego okna.
 * Przyciągamy je do NAJBLIŻSZYCH punktów, które faktycznie istnieją w serii, żeby zakres
 * w „Konfiguracji" zawsze pokrywał się z tym, co widać na wykresie — a nie z ułamkiem dnia,
 * którego backend i tak nie rozumie (`DateOnly`, patrz `BudgetPoint`).
 *
 * Zwraca `null`, gdy zaznaczenie jest węższe niż odstęp między dwoma sąsiednimi punktami
 * (nic sensownego do pokazania) albo gdy seria jest pusta.
 */
export function snapToAvailableRange(
  points: readonly { date: string }[],
  minMs: number,
  maxMs: number,
): [Date, Date] | null {
  if (points.length === 0) return null;

  const timestamps = points.map((p) => fromIsoDate(p.date).getTime()).sort((a, b) => a - b);

  const from = timestamps.find((t) => t >= minMs) ?? timestamps[0];
  const to = [...timestamps].reverse().find((t) => t <= maxMs) ?? timestamps[timestamps.length - 1];

  return from < to ? [new Date(from), new Date(to)] : null;
}
