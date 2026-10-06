import { StrategyEdge, StrategyEdgeLabel, StrategyNode } from '../../core/api/models/strategies';
import { canReceive } from './strategy-node-meta';

/** Krok łańcucha: kafelek, gałąź, którą do niego doszliśmy (tak / nie), i wcięcie. */
export interface ChainStep<T extends { readonly node: StrategyNode }> {
  readonly view: T;
  /** `None` dla zwykłej strzałki; `Yes` / `No` dla wyjścia z warunku. */
  readonly label: StrategyEdgeLabel;
  /** 0 = prosto za zdarzeniem; każda gałąź warunku zagłębia o jeden poziom. */
  readonly depth: number;
}

/** Zdarzenie i wszystko, do czego z niego dochodzą strzałki — widok „Lista kroków” na telefonie. */
export interface Chain<T extends { readonly node: StrategyNode }> {
  readonly root: T;
  readonly steps: readonly ChainStep<T>[];
}

export interface Chains<T extends { readonly node: StrategyNode }> {
  readonly chains: readonly Chain<T>[];
  /** Kafelki, do których nie dochodzi żaden łańcuch (np. akcja bez strzałki wejściowej). */
  readonly unlinked: readonly T[];
}

const BRANCH_ORDER: Record<StrategyEdgeLabel, number> = { Yes: 0, No: 1, None: 2 };

/**
 * Czyta graf tablicy jak listę: każdy kafelek, który niczego nie przyjmuje (zdarzenie), otwiera łańcuch, a kroki to
 * kafelki osiągalne strzałkami, w kolejności „tak”, „nie”, zwykła.
 *
 * ⚠️ Kafelek trafia do listy RAZ, nawet gdy prowadzi do niego kilka strzałek — wtedy zostaje przy pierwszym łańcuchu.
 * Dzięki temu pętla „Czekaj → Warunek” nie zapętla widoku, a nic się nie dubluje. Strzałka wsteczna nie ma osobnego
 * wiersza; widać ją na tablicy.
 */
export function buildChains<T extends { readonly node: StrategyNode }>(
  views: readonly T[],
  edges: readonly StrategyEdge[],
): Chains<T> {
  const byId = new Map(views.map((v) => [v.node.id, v]));
  const outgoing = new Map<string, StrategyEdge[]>();
  for (const e of edges) outgoing.set(e.from, [...(outgoing.get(e.from) ?? []), e]);
  for (const list of outgoing.values()) list.sort((a, b) => BRANCH_ORDER[a.label] - BRANCH_ORDER[b.label]);

  const roots = views
    .filter((v) => !canReceive(v.node.type))
    .sort((a, b) => a.node.y - b.node.y || a.node.x - b.node.x);

  const seen = new Set<string>(roots.map((r) => r.node.id));

  const chains = roots.map<Chain<T>>((root) => {
    const steps: ChainStep<T>[] = [];
    const walk = (fromId: string, depth: number): void => {
      for (const edge of outgoing.get(fromId) ?? []) {
        const view = byId.get(edge.to);
        if (!view || seen.has(view.node.id)) continue;
        seen.add(view.node.id);
        const stepDepth = edge.label === 'None' ? depth : depth + 1;
        steps.push({ view, label: edge.label, depth: stepDepth });
        walk(view.node.id, stepDepth);
      }
    };
    walk(root.node.id, 0);
    return { root, steps };
  });

  const unlinked = views
    .filter((v) => !seen.has(v.node.id))
    .sort((a, b) => a.node.y - b.node.y || a.node.x - b.node.x);

  return { chains, unlinked };
}
