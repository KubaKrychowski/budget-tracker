import { SaveStrategyRequest, StrategyEdge, StrategyNode } from '../../core/api/models/strategies';

/** Co się zmieniło na tablicy od ostatniego zapisu — same liczby, bez treści (tłumaczenia dokłada ekran). */
export interface ChangeSummary {
  readonly nodesAdded: number;
  readonly nodesRemoved: number;
  /** Kafelki, które są w obu wersjach, ale mają inną pozycję albo parametry. */
  readonly nodesChanged: number;
  readonly edgesAdded: number;
  readonly edgesRemoved: number;
  /** Czy zmieniła się lista wariantów albo ich wyłączenia. */
  readonly variantsChanged: boolean;
  /** Czy zmieniła się nazwa, miesiąc startu, oszczędności na początku albo horyzont. */
  readonly paramsChanged: boolean;
}

/** Jedna pozycja opisu zmian: klucz tłumaczenia (po `strategies.leave.changes.`) i liczba do wstawienia w tekst. */
export interface ChangeLine {
  readonly key: keyof ChangeSummary;
  readonly count: number;
}

const sameJson = (a: unknown, b: unknown): boolean => JSON.stringify(a) === JSON.stringify(b);

const byId = <T extends { readonly id: string }>(items: readonly T[]): Map<string, T> => new Map(items.map((i) => [i.id, i]));

/** Krawędź to ta sama strzałka, gdy zgadza się początek, koniec i etykieta — identyfikator nadaje klient. */
const edgeKey = (e: StrategyEdge): string => `${e.from}>${e.to}:${e.label}`;

/**
 * Porównuje zapisaną strategię z roboczym stanem tablicy — to, co pokazuje okno „Wyjść bez zapisania?”.
 *
 * Czysta funkcja, żeby reguły („przesunięcie to zmiana kafelka”, „ta sama strzałka pod nowym identyfikatorem to nie zmiana”)
 * dało się sprawdzić bez ekranu.
 */
export function summarizeChanges(saved: SaveStrategyRequest, working: SaveStrategyRequest): ChangeSummary {
  const before = byId(saved.nodes);
  const after = byId(working.nodes);
  let nodesChanged = 0;
  for (const [id, node] of after) {
    const old: StrategyNode | undefined = before.get(id);
    if (old && !sameJson(old, node)) nodesChanged++;
  }

  const oldEdges = new Set(saved.edges.map(edgeKey));
  const newEdges = new Set(working.edges.map(edgeKey));

  return {
    nodesAdded: [...after.keys()].filter((id) => !before.has(id)).length,
    nodesRemoved: [...before.keys()].filter((id) => !after.has(id)).length,
    nodesChanged,
    edgesAdded: [...newEdges].filter((k) => !oldEdges.has(k)).length,
    edgesRemoved: [...oldEdges].filter((k) => !newEdges.has(k)).length,
    variantsChanged: !sameJson(saved.variants, working.variants),
    paramsChanged: !sameJson(
      [saved.name.trim(), saved.startMonth, saved.startCash, saved.horizonMonths],
      [working.name.trim(), working.startMonth, working.startCash, working.horizonMonths],
    ),
  };
}

/** Niezerowe pozycje podsumowania, w stałej kolejności — do wypisania w oknie. */
export function describeChanges(summary: ChangeSummary): ChangeLine[] {
  const counted: (keyof ChangeSummary)[] = ['nodesAdded', 'nodesRemoved', 'nodesChanged', 'edgesAdded', 'edgesRemoved'];
  const lines: ChangeLine[] = counted
    .filter((key) => (summary[key] as number) > 0)
    .map((key) => ({ key, count: summary[key] as number }));
  if (summary.variantsChanged) lines.push({ key: 'variantsChanged', count: 1 });
  if (summary.paramsChanged) lines.push({ key: 'paramsChanged', count: 1 });
  return lines;
}
