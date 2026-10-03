import { registerLocaleData } from '@angular/common';
import pl from '@angular/common/locales/pl';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { APP_ICONS } from '../../core/icons';
import {
  Strategy, StrategyEdge, StrategyNode, StrategyProblem, StrategyResult,
} from '../../core/api/models/strategies';
import { StrategyBoard } from './strategy-board';

registerLocaleData(pl);

/** Dostęp do chronionych członków — testy sterują tablicą tak jak zdarzenia z biblioteki. */
interface BoardApi {
  addNode(type: string, at?: { x: number; y: number }): void;
  onCreateConnection(event: { sourceId: string; targetId?: string }): void;
  onDeleteSelected(event: { nodeIds: string[]; groupIds: string[]; connectionIds: string[] }): void;
  onMoveNodes(event: { nodes: { id: string; position: { x: number; y: number } }[] }): void;
  updateNode(node: StrategyNode): void;
  save(): Promise<void>;
  discard(): void;
  nodes(): readonly StrategyNode[];
  edges(): readonly StrategyEdge[];
  dirty(): boolean;
}

/**
 * Ekran „Strategia” — tablica. Biblioteka tablicy (`@foblex/flow`) niczego nie trzyma: wszystko wraca do komponentu jako
 * zdarzenie, więc testy podają mu te same zdarzenia. Geometrii (położenia, strzałek) jsdom nie liczy — to sprawdza przegląd
 * w przeglądarce.
 */
describe('StrategyBoard', () => {
  let fixture: ComponentFixture<StrategyBoard>;
  let http: HttpTestingController;

  const node = (over: Partial<StrategyNode>): StrategyNode => ({
    id: 'x', type: 'Trigger', title: '', x: 0, y: 0, month: null, amount: null, rate: null, installment: null,
    mode: null, metric: null, comparison: null, threshold: null, ...over,
  });

  const nodes: StrategyNode[] = [
    node({ id: 'loan', type: 'Loan', title: 'Kredyt', month: '2026-10-01', amount: 18400, rate: 12, installment: 880 }),
    node({ id: 'bonus', type: 'Income', title: 'Premia', month: '2027-05-01', amount: 7800 }),
    node({ id: 'overpay', type: 'Overpay', title: 'Nadpłać kredyt', amount: 7000, mode: 'ReduceInstallment' }),
    node({ id: 'check', type: 'Condition', title: 'Warunek', metric: 'CashMinusDebt', comparison: 'AtLeast', threshold: 9000 }),
    node({ id: 'payoff', type: 'PayOffLoan', title: 'Spłać resztę' }),
    node({ id: 'wait', type: 'Wait', title: 'Czekaj' }),
    node({ id: 'end', type: 'End', title: 'Koniec' }),
  ];

  const edges: StrategyEdge[] = [
    { id: 'e1', from: 'bonus', to: 'overpay', label: 'None' },
    { id: 'e2', from: 'overpay', to: 'check', label: 'None' },
    { id: 'e3', from: 'check', to: 'payoff', label: 'Yes' },
    { id: 'e4', from: 'check', to: 'wait', label: 'No' },
    { id: 'e5', from: 'wait', to: 'check', label: 'None' },
  ];

  const result = (problems: StrategyProblem[] = []): StrategyResult => ({
    months: [], loanPaidOffIn: '2027-07-01', cushionReachedIn: null, finalCash: 17300, totalInterest: 1400,
    nodes: [{ nodeId: 'check', firedIn: '2027-05-01', conditionMetIn: '2027-07-01' }], problems,
  });

  const strategy = (over: Partial<Strategy> = {}): Strategy => ({
    id: 's1', budgetId: 'b1', name: 'Kredyt i poduszka', startMonth: '2026-10-01', startCash: 4600, horizonMonths: 24,
    updatedAt: '2026-10-03T10:00:00+00:00', nodes, edges, result: result(), ...over,
  });

  const api = (): BoardApi => fixture.componentInstance as unknown as BoardApi;
  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');
  const nodeCount = (): number => fixture.nativeElement.querySelectorAll('.f-node').length;

  const settle = async (body: Strategy = strategy()): Promise<void> => {
    fixture.detectChanges();
    http.match((r) => r.url === '/api/strategies/s1' && r.method === 'GET').forEach((r) => r.flush(body));
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    /*
     * ⚠️ jsdom nie liczy układu, więc biblioteka tablicy zgłasza dla każdego złącza FF1006 („ukryte przez CSS”, geometria 0×0).
     * To fałszywy alarm środowiska testowego — przegląd w prawdziwej przeglądarce nie pokazuje żadnego ostrzeżenia.
     */
    const warn = console.warn.bind(console);
    vi.spyOn(console, 'warn').mockImplementation((...args: unknown[]) => {
      if (!String(args[0]).startsWith('[f-flow]')) warn(...args);
    });

    await TestBed.configureTestingModule({
      imports: [StrategyBoard],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'dashboard', children: [] }, { path: 'strategies', children: [] }]),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
        provideNzI18n(pl_PL),
        provideNzDateFnsAdapter(),
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ id: 's1' })), snapshot: { paramMap: convertToParamMap({ id: 's1' }) } },
        },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      dashboard: { breadcrumb: 'Dashboard' },
      strategies: {
        title: 'Strategie',
        errors: { loadFailed: 'Nie udało się wczytać strategii', saveFailed: 'Nie udało się zapisać zmian' },
        board: {
          lead: 'Połącz zdarzenia z akcjami.',
          params: 'Parametry', discard: 'Odrzuć zmiany', save: 'Zapisz strategię', unsaved: 'Są niezapisane zmiany',
          saved: 'Strategia zapisana.',
          problemsBanner: 'Problemy w tablicy: {{count}}', problemsBannerBody: 'Symulacja pomija kafelki.', showProblems: 'Pokaż problemy',
          problemsTitle: 'Problemy ({{count}})', openNode: 'Otwórz kafelek',
          problem: { NoIncomingEdge: 'Żadna strzałka tu nie prowadzi.', MissingParameter: 'Brakuje parametru.' },
          yes: 'tak', no: 'nie', edgeLabel: 'Połączenie: {{from}} → {{to}}', perMonth: '{{amount}} / mies.',
          sim: { title: 'Symulacja', loan: 'kredyt spłacony', cushion: 'poduszka osiągnięta', cash: 'gotówka na koniec', never: 'nie w tym horyzoncie' },
          palette: { title: 'Dodaj kafelek', tabs: { events: 'Zdarzenia', actions: 'Akcje', controls: 'Warunki' }, search: 'Szukaj', hint: 'Podpowiedź', add: 'Dodaj kafelek: {{name}}' },
          types: { Trigger: 'Zdarzenie', Income: 'Wpływ', Overpay: 'Nadpłać kredyt', PayOffLoan: 'Spłać resztę', Condition: 'Warunek', Wait: 'Czekaj', End: 'Koniec', IncreaseSurplus: 'Zwiększ nadwyżkę' },
          hints: {}, mode: { ReduceInstallment: 'Obniż ratę' }, metric: { CashMinusDebt: 'Gotówka − dług' },
        },
      },
    });
    translate.use('pl');

    fixture = TestBed.createComponent(StrategyBoard);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
  });

  it('wczytuje strategię i rysuje wszystkie kafelki z tytułem oraz wynikiem symulacji', async () => {
    await settle();

    expect(text()).toContain('Kredyt i poduszka');
    expect(nodeCount()).toBe(nodes.length);
    expect(fixture.nativeElement.querySelectorAll('f-connection')).toHaveLength(edges.length);
    expect(text()).toContain('Nadpłać kredyt');
    expect(text()).toContain('kredyt spłacony: lipiec 2027');
    expect(text()).toContain('gotówka na koniec');
  });

  it('warunek ma dwa wyjścia (tak i nie), a zwykła akcja jedno', async () => {
    await settle();

    const ports = (title: string): number => {
      const el = [...fixture.nativeElement.querySelectorAll('.f-node')].find((n) => (n as HTMLElement).textContent?.includes(title)) as HTMLElement;
      return el.querySelectorAll('.f-connector-source').length;
    };

    expect(ports('Warunek')).toBe(2);
    expect(ports('Nadpłać kredyt')).toBe(1);
    expect(ports('Koniec')).toBe(0);
  });

  it('bez zmian nie pokazuje „niezapisane zmiany”, a przyciski zapisu są wyłączone', async () => {
    await settle();

    expect(text()).not.toContain('Są niezapisane zmiany');
    const buttons = [...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[];
    expect(buttons.find((b) => b.textContent?.includes('Zapisz strategię'))!.disabled).toBe(true);
    expect(buttons.find((b) => b.textContent?.includes('Odrzuć zmiany'))!.disabled).toBe(true);
  });

  it('dodanie kafelka zaznacza zmianę, a „Zapisz” wysyła całą strategię jednym PUT', async () => {
    await settle();

    api().addNode('IncreaseSurplus', { x: 100, y: 200 });
    fixture.detectChanges();
    expect(nodeCount()).toBe(nodes.length + 1);
    expect(text()).toContain('Są niezapisane zmiany');

    const done = api().save();
    const put = http.expectOne((r) => r.url === '/api/strategies/s1' && r.method === 'PUT');
    expect(put.request.body.nodes).toHaveLength(nodes.length + 1);
    expect(put.request.body.edges).toHaveLength(edges.length);
    expect(put.request.body.name).toBe('Kredyt i poduszka');
    put.flush(strategy({ nodes: put.request.body.nodes, updatedAt: '2026-10-03T12:00:00+00:00' }));
    await done;
    fixture.detectChanges();

    expect(api().dirty()).toBe(false);
    expect(text()).not.toContain('Są niezapisane zmiany');
  });

  it('„Odrzuć zmiany” wraca do zapisanego stanu', async () => {
    await settle();

    api().addNode('IncreaseSurplus', { x: 100, y: 200 });
    api().onDeleteSelected({ nodeIds: ['check'], groupIds: [], connectionIds: [] });
    fixture.detectChanges();
    expect(api().nodes()).toHaveLength(nodes.length);

    api().discard();
    fixture.detectChanges();

    expect(api().nodes().map((n) => n.id)).toEqual(nodes.map((n) => n.id));
    expect(api().edges()).toHaveLength(edges.length);
    expect(api().dirty()).toBe(false);
  });

  it('zmiana kafelka prosi serwer o symulację niezapisanego grafu', async () => {
    await settle();

    api().updateNode({ ...nodes.find((n) => n.id === 'overpay')!, amount: 5000 });
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 450));

    const simulate = http.expectOne((r) => r.url === '/api/strategies/simulate' && r.method === 'POST');
    expect(simulate.request.body.nodes.find((n: StrategyNode) => n.id === 'overpay').amount).toBe(5000);
    simulate.flush(result());
  });

  it('połączenie z wyjścia „tak” warunku dostaje etykietę Yes, a z wyjścia „nie” — No', async () => {
    await settle();
    const before = api().edges().length;

    api().onCreateConnection({ sourceId: 'check-out-yes', targetId: 'end-in' });
    api().onCreateConnection({ sourceId: 'check-out-no', targetId: 'end-in' });

    const added = api().edges().slice(before);
    expect(added.map((e) => [e.from, e.to, e.label])).toEqual([['check', 'end', 'Yes'], ['check', 'end', 'No']]);
  });

  it('odrzuca powtórkę, pętlę na samym sobie i połączenia, których rodzaje kafelków nie dopuszczają', async () => {
    await settle();
    const before = api().edges().length;

    api().onCreateConnection({ sourceId: 'bonus-out', targetId: 'overpay-in' });
    api().onCreateConnection({ sourceId: 'overpay-out', targetId: 'overpay-in' });
    api().onCreateConnection({ sourceId: 'end-out', targetId: 'overpay-in' });
    api().onCreateConnection({ sourceId: 'overpay-out', targetId: 'bonus-in' });
    api().onCreateConnection({ sourceId: 'overpay-out' });

    expect(api().edges()).toHaveLength(before);
  });

  it('usunięcie kafelka zabiera jego połączenia, a usunięcie połączenia zostawia kafelki', async () => {
    await settle();

    api().onDeleteSelected({ nodeIds: ['check'], groupIds: [], connectionIds: ['e1'] });

    expect(api().nodes().map((n) => n.id)).not.toContain('check');
    expect(api().edges().map((e) => e.id)).toEqual([]);
    expect(api().nodes()).toHaveLength(nodes.length - 1);
  });

  it('przesunięcie kafelka zapisuje nową pozycję i oznacza zmianę', async () => {
    await settle();

    api().onMoveNodes({ nodes: [{ id: 'bonus', position: { x: 120.4, y: 80.6 } }] });

    const moved = api().nodes().find((n) => n.id === 'bonus')!;
    expect([moved.x, moved.y]).toEqual([120, 81]);
    expect(api().dirty()).toBe(true);
  });

  it('pokazuje baner z liczbą kafelków z problemami', async () => {
    await settle(strategy({ result: result([{ nodeId: 'payoff', kind: 'NoIncomingEdge' }]) }));

    expect(text()).toContain('Problemy w tablicy: 1');
    expect(fixture.nativeElement.querySelectorAll('.sb__badge')).toHaveLength(1);
  });

  it('błąd wczytania pokazuje komunikat z możliwością ponowienia', async () => {
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/strategies/s1').flush({}, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text()).toContain('Nie udało się wczytać strategii');
  });
});
