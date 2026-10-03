import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom, map } from 'rxjs';
import {
  FCanvasComponent, FCreateConnectionEvent, FCreateNodeEvent, FDeleteSelectedEvent, FFlowModule, FMoveNodesEvent,
  FSelectionChangeEvent, provideFFlow, withA11y, withConnectionFlow,
} from '@foblex/flow';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ErrorMessages } from '../../core/errors/error-messages';
import { errorOf, valueOf } from '../../core/api/resource-value';
import { parseAmount } from '../../core/parse-amount';
import {
  SaveStrategyRequest, Strategy, StrategyEdge, StrategyEdgeLabel, StrategyNode, StrategyNodeType, StrategyProblemKind,
  StrategyResult,
} from '../../core/api/models/strategies';
import {
  canReceive, canSend, inputConnector, newNode, NODE_META, outputConnector, parseInputConnector, parseOutputConnector,
} from './strategy-node-meta';
import { StrategyNodeForm } from './strategy-node-form';
import { StrategyPalette } from './strategy-palette';

/** Opóźnienie podglądu symulacji — żeby nie pytać serwera przy każdym pikselu przeciągania. */
const SIMULATE_DEBOUNCE_MS = 400;

/** Odstęp kafelków na tablicy, gdy kafelek dodaje przycisk „+” zamiast upuszczenia. */
const GRID_X = 280;
const GRID_Y = 110;

/** Najdłuższa nazwa strategii — ta sama co `StrategyGraphValidator.MaxNameLength` w API. */
const MAX_NAME_LENGTH = 100;

/** Kafelek gotowy do narysowania — wszystko, co szablon tablicy musi o nim wiedzieć. */
interface BoardNode {
  readonly node: StrategyNode;
  readonly position: { x: number; y: number };
  readonly kind: (typeof NODE_META)[StrategyNodeType]['kind'];
  readonly title: string;
  readonly summary: string;
  readonly receives: boolean;
  readonly sends: boolean;
  readonly problems: readonly StrategyProblemKind[];
}

/**
 * Ekran „Strategia” — tablica kafelków połączonych strzałkami (makieta Figma, strona „Strategia”: 377:97, ustawienia
 * węzłów 380:415 / 385:246 / 385:532 / 385:812, błędy 386:301, menu 386:575; zgłoszenie #27).
 *
 * Użytkownik układa na tablicy zdarzenia, akcje i warunki, a serwer liczy z nich gotówkę i dług miesiąc po miesiącu.
 * Ten komponent trzyma ROBOCZY stan tablicy (nazwa, parametry, kafelki, połączenia) i na bieżąco prosi serwer
 * o symulację (`POST /api/strategies/simulate`, nic nie zapisuje); „Zapisz” wysyła całość jednym `PUT`.
 *
 * ⚠️ Biblioteka tablicy (`@foblex/flow`) NIE trzyma grafu — wszystko, co użytkownik zrobi (przesunięcie, połączenie,
 * upuszczenie z palety, usunięcie), wraca do tego komponentu jako zdarzenie, a on aktualizuje sygnały i dopiero
 * wtedy szablon rysuje nowy stan. Dzięki temu „Odrzuć zmiany” i symulacja mają jedno źródło prawdy.
 *
 * Nie ma jeszcze: „Zastosuj w budżecie” (etap 3), wariantów i wykresu (etap 2), polskich komunikatów czytnika ekranu
 * (biblioteka mówi po angielsku, ale nazwy połączeń niosą już tytuły kafelków).
 */
@Component({
  selector: 'app-strategy-board',
  imports: [
    DatePipe, FormsModule, RouterLink, FFlowModule,
    NzAlertModule, NzBreadCrumbModule, NzButtonModule, NzDatePickerModule, NzInputModule, NzInputNumberModule,
    NzModalModule, NzSpinModule, TranslatePipe, StrategyNodeForm, StrategyPalette,
  ],
  providers: [provideFFlow(withA11y(), withConnectionFlow('click'))],
  templateUrl: './strategy-board.html',
  styleUrl: './strategy-board.scss',
})
export class StrategyBoard {
  /** Parser polskiego formatu kwot dla pól `nz-input-number` — uzasadnienie przy `parseAmount`. */
  protected readonly parseAmount = parseAmount;
  protected readonly maxNameLength = MAX_NAME_LENGTH;

  /** Język nazw miesięcy w szablonie — `DatePipe` bez tego pokazuje angielskie. */
  protected locale(): string {
    return this.translate.currentLang() || 'pl';
  }

  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly errorMessages = inject(ErrorMessages);

  private readonly canvas = viewChild(FCanvasComponent);

  private readonly id = toSignal(this.route.paramMap.pipe(map((p) => p.get('id'))), {
    initialValue: this.route.snapshot.paramMap.get('id'),
  });

  private readonly resource = httpResource<Strategy>(() => {
    const id = this.id();
    return id ? `/api/strategies/${id}` : undefined;
  });

  /** `value()` RZUCA w stanie błędu — patrz core/api/resource-value.ts. */
  private readonly loaded = valueOf(this.resource);

  protected readonly loading = this.resource.isLoading;
  protected readonly failure = errorOf(this.resource);

  // ── Stan roboczy ─────────────────────────────────────────────────────────────────────

  /** Ostatnio zapisana strategia — punkt odniesienia dla „Odrzuć zmiany” i znacznika niezapisanych zmian. */
  protected readonly saved = signal<Strategy | null>(null);
  protected readonly name = signal('');
  protected readonly startMonth = signal('');
  protected readonly startCash = signal<number | null>(0);
  protected readonly horizonMonths = signal(24);
  protected readonly nodes = signal<readonly StrategyNode[]>([]);
  protected readonly edges = signal<readonly StrategyEdge[]>([]);
  protected readonly result = signal<StrategyResult | null>(null);
  protected readonly busy = signal(false);

  private readonly request = computed<SaveStrategyRequest>(() => ({
    name: this.name().trim(),
    startMonth: this.startMonth(),
    startCash: this.startCash() ?? 0,
    horizonMonths: this.horizonMonths(),
    nodes: this.nodes(),
    edges: this.edges(),
  }));

  private readonly signature = computed(() => JSON.stringify(this.request()));
  private readonly savedSignature = signal('');
  protected readonly dirty = computed(() => this.saved() !== null && this.signature() !== this.savedSignature());

  private simulationRun = 0;

  /** Pierwsze załadowanie (i każde kolejne przy braku zmian) kopiuje strategię do stanu roboczego. */
  private readonly adopt = effect(() => {
    const strategy = this.loaded();
    if (strategy && !untracked(() => this.dirty())) untracked(() => this.load(strategy));
  });

  /** Podgląd symulacji niezapisanego grafu — z opóźnieniem, a odpowiedź spóźniona nie nadpisuje nowszej. */
  private readonly simulate = effect((onCleanup) => {
    const signature = this.signature();
    const saved = this.saved();
    if (!saved) return;
    if (signature === untracked(this.savedSignature)) {
      untracked(() => this.result.set(saved.result));
      return;
    }

    const body = untracked(this.request);
    const timer = setTimeout(() => void this.runSimulation(body), SIMULATE_DEBOUNCE_MS);
    onCleanup(() => clearTimeout(timer));
  });

  private load(strategy: Strategy): void {
    this.saved.set(strategy);
    this.name.set(strategy.name);
    this.startMonth.set(strategy.startMonth);
    this.startCash.set(strategy.startCash);
    this.horizonMonths.set(strategy.horizonMonths);
    this.nodes.set(strategy.nodes);
    this.edges.set(strategy.edges);
    this.result.set(strategy.result);
    this.savedSignature.set(JSON.stringify({
      name: strategy.name.trim(),
      startMonth: strategy.startMonth,
      startCash: strategy.startCash,
      horizonMonths: strategy.horizonMonths,
      nodes: strategy.nodes,
      edges: strategy.edges,
    }));
  }

  private async runSimulation(body: SaveStrategyRequest): Promise<void> {
    const run = ++this.simulationRun;
    try {
      const next = await firstValueFrom(this.http.post<StrategyResult>('/api/strategies/simulate', body));
      if (run === this.simulationRun) this.result.set(next);
    } catch {
      // Graf, którego nie da się policzyć, zostawia poprzedni wynik — komunikat dałby błysk przy każdym ruchu.
    }
  }

  // ── Widok tablicy ────────────────────────────────────────────────────────────────────

  private readonly problemsByNode = computed(() => {
    const map = new Map<string, StrategyProblemKind[]>();
    for (const p of this.result()?.problems ?? []) map.set(p.nodeId, [...(map.get(p.nodeId) ?? []), p.kind]);
    return map;
  });

  protected readonly viewNodes = computed<readonly BoardNode[]>(() =>
    this.nodes().map((n) => ({
      node: n,
      position: { x: n.x, y: n.y },
      kind: NODE_META[n.type].kind,
      title: this.titleOf(n),
      summary: this.summaryOf(n),
      receives: canReceive(n.type),
      sends: canSend(n.type),
      problems: this.problemsByNode().get(n.id) ?? [],
    })));

  protected readonly problemNodes = computed(() => this.viewNodes().filter((n) => n.problems.length > 0));

  protected problemsByNodeOf(id: string): readonly StrategyProblemKind[] {
    return this.problemsByNode().get(id) ?? [];
  }

  protected sourceConnector(edge: StrategyEdge): string {
    return outputConnector(edge.from, edge.label);
  }

  protected targetConnector(edge: StrategyEdge): string {
    return inputConnector(edge.to);
  }

  /** Nazwa połączenia dla czytnika ekranu — tytuły kafelków zamiast identyfikatorów złączy. */
  protected edgeLabel(edge: StrategyEdge): string {
    const from = this.nodes().find((n) => n.id === edge.from);
    const to = this.nodes().find((n) => n.id === edge.to);
    return this.translate.instant('strategies.board.edgeLabel', {
      from: from ? this.titleOf(from) : edge.from,
      to: to ? this.titleOf(to) : edge.to,
    });
  }

  protected titleOf(node: StrategyNode): string {
    return node.title.trim() || this.translate.instant(`strategies.board.types.${node.type}`);
  }

  protected glyph(kind: BoardNode['kind']): string {
    return kind === 'event' || kind === 'base' ? '⚡' : kind === 'condition' ? '?' : kind === 'end' ? '✓' : kind === 'wait' ? '⏱' : '</>';
  }

  private money(value: number | null | undefined): string {
    return value === null || value === undefined
      ? '—'
      : `${new Intl.NumberFormat(this.translate.currentLang() || 'pl', { maximumFractionDigits: 2 }).format(value)} zł`;
  }

  private monthLabel(month: string | null): string {
    if (!month) return '—';
    const [year, m] = month.split('-').map(Number);
    return new Intl.DateTimeFormat(this.translate.currentLang() || 'pl', { month: 'long', year: 'numeric' }).format(new Date(year, m - 1, 1));
  }

  /** Jedna linia pod tytułem kafelka — to, co najważniejsze w jego parametrach. */
  private summaryOf(n: StrategyNode): string {
    switch (n.type) {
      case 'Trigger': return this.monthLabel(n.month);
      case 'Income': return `+${this.money(n.amount)} · ${this.monthLabel(n.month)}`;
      case 'Expense': return `−${this.money(n.amount)} · ${this.monthLabel(n.month)}`;
      case 'Surplus': return this.translate.instant('strategies.board.perMonth', { amount: this.money(n.amount) });
      case 'IncreaseSurplus':
        return this.translate.instant('strategies.board.perMonth', { amount: `${(n.amount ?? 0) > 0 ? '+' : ''}${this.money(n.amount)}` });
      case 'Loan':
        return `${this.money(n.amount)} · ${n.rate ?? '—'}% · ${this.translate.instant('strategies.board.installmentShort', { amount: this.money(n.installment) })}`;
      case 'Overpay':
        return `${this.money(n.amount)} · ${this.translate.instant(`strategies.board.mode.${n.mode ?? 'ReduceInstallment'}`)}`;
      case 'CushionGoal':
      case 'SetSavingsGoal':
      case 'CreateReservation':
      case 'SetLimit': return this.money(n.amount);
      case 'Condition':
        return `${this.translate.instant(`strategies.board.metric.${n.metric ?? 'CashMinusDebt'}`)} ${n.comparison === 'AtMost' ? '≤' : '≥'} ${this.money(n.threshold)}`;
      default: return '';
    }
  }

  // ── Podsumowanie symulacji ───────────────────────────────────────────────────────────

  protected readonly hasLoan = computed(() => this.nodes().some((n) => n.type === 'Loan'));
  protected readonly hasCushion = computed(() => this.nodes().some((n) => n.type === 'CushionGoal'));

  protected readonly finalCash = computed(() => this.money(this.result()?.finalCash));

  protected fitToScreen(): void {
    this.canvas()?.fitToScreen({ x: 40, y: 40 }, false, false, 1);
  }

  // ── Zaznaczenie i panel ──────────────────────────────────────────────────────────────

  private readonly selectedNodeIds = signal<readonly string[]>([]);
  protected readonly selectedConnectionIds = signal<readonly string[]>([]);

  /** „← Dodaj kafelek” w formularzu węzła chowa formularz do czasu następnego zaznaczenia. */
  private readonly formDismissed = signal(false);

  protected readonly panel = signal<'palette' | 'problems'>('palette');

  protected readonly selectedNode = computed(() => {
    const ids = this.selectedNodeIds();
    return ids.length === 1 ? (this.nodes().find((n) => n.id === ids[0]) ?? null) : null;
  });

  protected readonly formNode = computed(() => (this.formDismissed() ? null : this.selectedNode()));

  protected readonly selectedEdge = computed(() => {
    const ids = this.selectedConnectionIds();
    return ids.length === 1 && this.selectedNodeIds().length === 0 ? (this.edges().find((e) => e.id === ids[0]) ?? null) : null;
  });

  protected readonly formOutcome = computed(() => {
    const node = this.formNode();
    return node ? (this.result()?.nodes.find((o) => o.nodeId === node.id) ?? null) : null;
  });

  protected onSelection(event: FSelectionChangeEvent): void {
    this.selectedNodeIds.set(event.nodeIds);
    this.selectedConnectionIds.set(event.connectionIds);
    this.formDismissed.set(false);
  }

  protected openNode(id: string): void {
    this.selectedNodeIds.set([id]);
    this.selectedConnectionIds.set([]);
    this.formDismissed.set(false);
  }

  protected closeForm(): void {
    this.formDismissed.set(true);
  }

  // ── Zmiany tablicy ───────────────────────────────────────────────────────────────────

  private uid(): string {
    return `n${Math.random().toString(36).slice(2, 10)}`;
  }

  /** Pierwsze wolne miejsce na kolejny kafelek: na prawo od zaznaczonego, a gdy zajęte — niżej. */
  private freePosition(): { x: number; y: number } {
    const anchor = this.selectedNode();
    const all = this.nodes();
    const x = anchor ? anchor.x + GRID_X : (all.length === 0 ? 24 : Math.min(...all.map((n) => n.x)));
    let y = anchor ? anchor.y : (all.length === 0 ? 40 : Math.max(...all.map((n) => n.y)) + GRID_Y);
    while (all.some((n) => Math.abs(n.x - x) < GRID_X / 2 && Math.abs(n.y - y) < GRID_Y / 2)) y += GRID_Y;
    return { x, y };
  }

  /** Dodaje kafelek: z palety przez „+” (obok zaznaczonego, z automatycznym połączeniem) albo upuszczeniem (w miejscu upuszczenia). */
  protected addNode(type: StrategyNodeType, at?: { x: number; y: number }): void {
    const from = this.selectedNode();
    const position = at ?? this.freePosition();
    const node = newNode(type, this.uid(), Math.round(position.x), Math.round(position.y), this.startMonth());
    this.nodes.update((all) => [...all, node]);

    if (!at && from && canSend(from.type) && canReceive(type)) {
      const label = this.freeBranch(from);
      if (label) this.addEdge(from.id, node.id, label);
    }
  }

  /** Wyjście, które nowa strzałka z kafelka może zająć: warunek ma tak i nie, reszta jedno. */
  private freeBranch(from: StrategyNode): StrategyEdgeLabel | null {
    if (from.type !== 'Condition') return 'None';
    const used = new Set(this.edges().filter((e) => e.from === from.id).map((e) => e.label));
    return !used.has('Yes') ? 'Yes' : !used.has('No') ? 'No' : null;
  }

  protected onCreateNode(event: FCreateNodeEvent<StrategyNodeType>): void {
    if (!NODE_META[event.data]) return;
    this.addNode(event.data, { x: event.externalItemRect.x, y: event.externalItemRect.y });
  }

  protected onMoveNodes(event: FMoveNodesEvent): void {
    const moved = new Map(event.nodes.map((n) => [n.id, n.position]));
    this.nodes.update((all) => all.map((n) => {
      const to = moved.get(n.id);
      return to ? { ...n, x: Math.round(to.x), y: Math.round(to.y) } : n;
    }));
  }

  protected onCreateConnection(event: FCreateConnectionEvent): void {
    if (!event.targetId) return;
    const source = parseOutputConnector(event.sourceId);
    const target = parseInputConnector(event.targetId);
    if (source && target) this.addEdge(source.nodeId, target, source.label);
  }

  /** Dodaje strzałkę; odrzuca powtórkę, pętlę na samym sobie i połączenia, których rodzaje kafelków nie dopuszczają. */
  private addEdge(from: string, to: string, label: StrategyEdgeLabel): void {
    if (from === to) return;
    const source = this.nodes().find((n) => n.id === from);
    const target = this.nodes().find((n) => n.id === to);
    if (!source || !target || !canSend(source.type) || !canReceive(target.type)) return;
    if (this.edges().some((e) => e.from === from && e.to === to && e.label === label)) return;
    this.edges.update((all) => [...all, { id: `e${Math.random().toString(36).slice(2, 10)}`, from, to, label }]);
  }

  protected updateNode(next: StrategyNode): void {
    this.nodes.update((all) => all.map((n) => (n.id === next.id ? next : n)));
  }

  protected removeNode(id: string): void {
    this.nodes.update((all) => all.filter((n) => n.id !== id));
    this.edges.update((all) => all.filter((e) => e.from !== id && e.to !== id));
    this.selectedNodeIds.update((ids) => ids.filter((x) => x !== id));
  }

  protected removeEdge(id: string): void {
    this.edges.update((all) => all.filter((e) => e.id !== id));
    this.selectedConnectionIds.update((ids) => ids.filter((x) => x !== id));
  }

  protected onDeleteSelected(event: FDeleteSelectedEvent): void {
    for (const id of event.nodeIds) this.removeNode(id);
    for (const id of event.connectionIds) this.removeEdge(id);
  }

  // ── Parametry strategii ──────────────────────────────────────────────────────────────

  protected readonly paramsOpen = signal(false);
  protected readonly draftName = signal('');
  protected readonly draftMonth = signal<Date | null>(null);
  protected readonly draftCash = signal<number | null>(0);
  protected readonly draftHorizon = signal<number | null>(24);

  protected readonly paramsValid = computed(() => {
    const name = this.draftName().trim();
    const horizon = this.draftHorizon();
    return name.length > 0 && name.length <= MAX_NAME_LENGTH && this.draftMonth() !== null
      && horizon !== null && horizon >= 6 && horizon <= 60;
  });

  protected openParams(): void {
    const [year, m] = this.startMonth().split('-').map(Number);
    this.draftName.set(this.name());
    this.draftMonth.set(new Date(year, m - 1, 1));
    this.draftCash.set(this.startCash());
    this.draftHorizon.set(this.horizonMonths());
    this.paramsOpen.set(true);
  }

  protected applyParams(): void {
    const month = this.draftMonth();
    if (!this.paramsValid() || !month) return;
    this.name.set(this.draftName().trim());
    this.startMonth.set(`${month.getFullYear()}-${String(month.getMonth() + 1).padStart(2, '0')}-01`);
    this.startCash.set(this.draftCash() ?? 0);
    this.horizonMonths.set(this.draftHorizon() ?? 24);
    this.paramsOpen.set(false);
  }

  // ── Zapis ────────────────────────────────────────────────────────────────────────────

  protected discard(): void {
    const saved = this.saved();
    if (saved) this.load(saved);
  }

  protected async save(): Promise<void> {
    const saved = this.saved();
    if (!saved || !this.dirty()) return;

    this.busy.set(true);
    try {
      const next = await firstValueFrom(this.http.put<Strategy>(`/api/strategies/${saved.id}`, this.request()));
      this.load(next);
      this.message.success(this.translate.instant('strategies.board.saved'));
    } catch (err) {
      this.message.error(this.errorMessages.of(err, 'strategies.errors.saveFailed'));
    } finally {
      this.busy.set(false);
    }
  }

  protected reload(): void {
    this.resource.reload();
  }

  protected errorText(err: unknown): string {
    return this.errorMessages.of(err, 'strategies.errors.loadFailed');
  }
}
