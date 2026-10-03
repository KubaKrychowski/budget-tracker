import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import {
  FCreateConnectionEvent,
  FCreateNodeEvent,
  FFlowModule,
  FMoveNodesEvent,
  provideFFlow,
  withA11y,
  withConnectionFlow,
} from '@foblex/flow';

/** Rodzaj kafelka — wystarczy do sprawdzenia, że biblioteka niesie różne węzły. */
type SpikeKind = 'event' | 'action' | 'condition';

interface SpikeNode {
  readonly id: string;
  readonly kind: SpikeKind;
  readonly title: string;
  readonly position: { x: number; y: number };
}

interface SpikeConnection {
  readonly id: string;
  readonly source: string;
  readonly target: string;
}

/** Kafelek z palety — `data` wraca w `fCreateNode` po upuszczeniu na tablicę. */
interface PaletteItem {
  readonly kind: SpikeKind;
  readonly title: string;
}

/**
 * SPIKE (gałąź spike/foblex-flow) — sprawdza, czy @foblex/flow działa na Angularze 22:
 * węzły z sygnałów, połączenia, przeciąganie z palety, tworzenie połączenia przeciągnięciem i
 * kliknięciem, klawiatura (withA11y). Nie jest funkcją produktu: teksty są w kodzie celowo,
 * komponent ma zniknąć razem z gałęzią albo zostać przepisany w etapie 1 kreatora strategii.
 */
@Component({
  selector: 'app-strategy-spike',
  imports: [FFlowModule],
  providers: [provideFFlow(withA11y(), withConnectionFlow('click'))],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './strategy-spike.html',
  styleUrl: './strategy-spike.scss',
})
export class StrategySpike {
  protected readonly palette: readonly PaletteItem[] = [
    { kind: 'event', title: 'Zdarzenie' },
    { kind: 'action', title: 'Akcja' },
    { kind: 'condition', title: 'Warunek' },
  ];

  protected readonly nodes = signal<readonly SpikeNode[]>([
    { id: 'n1', kind: 'event', title: 'Premia', position: { x: 40, y: 60 } },
    { id: 'n2', kind: 'action', title: 'Nadpłać kredyt', position: { x: 300, y: 60 } },
    { id: 'n3', kind: 'condition', title: 'Gotówka − dług ≥ X', position: { x: 560, y: 60 } },
  ]);

  protected readonly connections = signal<readonly SpikeConnection[]>([
    { id: 'c1', source: 'n1-out', target: 'n2-in' },
    { id: 'c2', source: 'n2-out', target: 'n3-in' },
  ]);

  private nextId = 4;

  /** Upuszczenie kafelka z palety — nowy węzeł w miejscu upuszczenia. */
  protected onCreateNode(event: FCreateNodeEvent<PaletteItem>): void {
    const id = `n${this.nextId++}`;
    this.nodes.update((all) => [
      ...all,
      {
        id,
        kind: event.data.kind,
        title: event.data.title,
        position: { x: event.externalItemRect.x, y: event.externalItemRect.y },
      },
    ]);
  }

  /** Nowe połączenie (przeciągnięciem albo kliknięciem) — odrzucamy upuszczenie w pustkę. */
  protected onCreateConnection(event: FCreateConnectionEvent): void {
    console.debug('[spike] fCreateConnection', event.sourceId, event.targetId);
    if (!event.targetId) return;
    const id = `c${this.nextId++}`;
    this.connections.update((all) => [...all, { id, source: event.sourceId, target: event.targetId! }]);
  }

  /** Przesunięcie węzłów — zapisujemy pozycje w stanie aplikacji (tryb klasyczny biblioteki). */
  protected onMoveNodes(event: FMoveNodesEvent): void {
    const moved = new Map(event.nodes.map((n) => [n.id, n.position]));
    this.nodes.update((all) => all.map((n) => (moved.has(n.id) ? { ...n, position: moved.get(n.id)! } : n)));
  }
}
