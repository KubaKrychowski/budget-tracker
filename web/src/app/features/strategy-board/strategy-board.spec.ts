import { registerLocaleData } from '@angular/common';
import pl from '@angular/common/locales/pl';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { APP_ICONS } from '../../core/icons';
import { Viewport } from '../../core/layout/viewport';
import {
  Strategy, StrategyEdge, StrategyNode, StrategyProblem, StrategyReferences, StrategyResult, StrategyVariantWithResult,
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
  changeType(id: string, type: string): void;
  incomingTypesOf(id: string): string[];
  save(): Promise<void>;
  discard(): void;
  nodes(): readonly StrategyNode[];
  edges(): readonly StrategyEdge[];
  dirty(): boolean;
  addNewVariant(): void;
  selectVariant(id: string | null): void;
  toggleTile(change: { nodeId: string; disabled: boolean }): void;
  removeVariant(id: string): void;
  removeNode(id: string): void;
  canLeave(): boolean | Promise<boolean>;
  stay(): void;
  discardAndLeave(): void;
  saveAndLeave(): Promise<void>;
  leaveOpen(): boolean;
  changeLines(): string[];
  onBeforeUnload(event: { preventDefault(): void }): void;
  variants(): readonly { id: string; name: string; disabledNodeIds: readonly string[] }[];
}

/**
 * Ekran „Strategia” — tablica. Biblioteka tablicy (`@foblex/flow`) niczego nie trzyma: wszystko wraca do komponentu jako
 * zdarzenie, więc testy podają mu te same zdarzenia. Geometrii (położenia, strzałek) jsdom nie liczy — to sprawdza przegląd
 * w przeglądarce.
 */
describe('StrategyBoard', () => {
  let fixture: ComponentFixture<StrategyBoard>;
  let http: HttpTestingController;
  /** Widok mobilny sterowany z testu — jsdom nie ma `matchMedia`, więc domyślnie jest desktop. */
  const mobile = signal(false);

  const node = (over: Partial<StrategyNode>): StrategyNode => ({
    id: 'x', type: 'Trigger', title: '', x: 0, y: 0, month: null, amount: null, rate: null, installment: null,
    mode: null, metric: null, comparison: null, threshold: null, categoryId: null, standingOrderId: null, actualMonth: null, actualAmount: null, ...over,
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
    updatedAt: '2026-10-03T10:00:00+00:00', nodes, edges, result: result(), variants: [], ...over,
  });

  const variant = (over: Partial<StrategyVariantWithResult> = {}): StrategyVariantWithResult => ({
    id: 'v1', name: 'Bez premii', disabledNodeIds: ['bonus'], result: { ...result(), finalCash: 14900, loanPaidOffIn: '2027-08-01' }, ...over,
  });

  const api = (): BoardApi => fixture.componentInstance as unknown as BoardApi;
  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');
  const nodeCount = (): number => fixture.nativeElement.querySelectorAll('.f-node').length;

  const references: StrategyReferences = {
    categories: [{ id: 'c1', name: 'Jedzenie' }],
    standingOrders: [{ id: 'o1', name: 'Rata kredytu', expectedAmount: 880 }],
  };

  const settle = async (body: Strategy = strategy()): Promise<void> => {
    fixture.detectChanges();
    http.match((r) => r.url === '/api/strategies/s1' && r.method === 'GET').forEach((r) => r.flush(body));
    fixture.detectChanges();
    await Promise.resolve();
    fixture.detectChanges();
    http.match((r) => r.url === '/api/strategies/s1/references').forEach((r) => r.flush(references));
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    mobile.set(false);
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
        { provide: Viewport, useValue: { isMobile: mobile.asReadonly() } },
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
        variants: {
          barLabel: 'Wariant', base: 'Bazowy', add: '+ Nowy wariant', defaultName: 'Wariant', copySuffix: 'kopia', manage: 'Warianty',
          tabTiles: 'Kafelki', tabVariants: 'Warianty', hint: 'Wariant to ten sam graf.', baseHint: 'Bazowy ma wszystko włączone.',
          loan: 'kredyt spłacony: {{month}}', cash: 'gotówka na koniec: {{amount}}', never: 'nie w tym horyzoncie', delta: '{{delta}} względem bazowego',
          duplicate: 'Duplikuj', rename: 'Zmień nazwę', remove: 'Usuń', tilesTitle: 'Kafelki w wariancie „{{name}}”',
          toggle: 'Kafelek „{{name}}” w wariancie', note: 'Przełączniki zmieniają tylko ten wariant.', noTiles: 'Brak kafelków.',
          renameTitle: 'Zmień nazwę wariantu', renameLabel: 'Nazwa wariantu', renameOk: 'Zapisz',
          errors: { required: 'Podaj nazwę.', tooLong: 'Za długa.', taken: 'Zajęta.' }, limit: 'Limit wariantów.',
        },
        errors: { loadFailed: 'Nie udało się wczytać strategii', saveFailed: 'Nie udało się zapisać zmian' },
        facts: { summary: 'Zdarzenia: {{done}} z {{total}} zrealizowane', done: 'nastąpiło', planned: 'planowane', note: 'Fakty liczone z faktu.' },
        leave: {
          title: 'Wyjść bez zapisania?', alertTitle: 'Strategia „{{name}}” ma niezapisane zmiany.', body: 'Zmiany przepadną.',
          stay: 'Zostań na tablicy', discard: 'Odrzuć i wyjdź', save: 'Zapisz i wyjdź',
          changes: { nodesAdded: 'dodane kafelki: {{count}}', nodesRemoved: 'usunięte kafelki: {{count}}', nodesChanged: 'zmienione kafelki: {{count}}',
            edgesAdded: 'dodane połączenia: {{count}}', edgesRemoved: 'usunięte połączenia: {{count}}', variantsChanged: 'zmiany w wariantach', paramsChanged: 'zmienione parametry strategii' },
        },
        board: {
          apply: 'Zastosuj w budżecie…',
          lead: 'Połącz zdarzenia z akcjami.',
          params: 'Parametry', discard: 'Odrzuć zmiany', save: 'Zapisz strategię', unsaved: 'Są niezapisane zmiany',
          saved: 'Strategia zapisana.',
          problemsBanner: 'Problemy w tablicy: {{count}}', problemsBannerBody: 'Symulacja pomija kafelki.', showProblems: 'Pokaż problemy',
          problemsTitle: 'Problemy ({{count}})', openNode: 'Otwórz kafelek',
          problem: { NoIncomingEdge: 'Żadna strzałka tu nie prowadzi.', MissingParameter: 'Brakuje parametru.' },
          mobile: { list: 'Lista kroków', board: 'Tablica', menu: 'Menu', tiles: 'Kafelki: {{count}}', addTile: '+ Dodaj kafelek', save: 'Zapisz', addStep: '+ Dodaj krok', unlinked: 'Bez połączenia', empty: 'Pusto', zoom: 'Powiększenie', zoomOut: 'Pomniejsz', zoomIn: 'Powiększ', fit: 'Dopasuj', gesture: 'Przesuń palcem', paletteHint: 'Dotknij +' },
          yes: 'tak', no: 'nie', edgeLabel: 'Połączenie: {{from}} → {{to}}', perMonth: '{{amount}} / mies.',
          sim: { title: 'Symulacja', facts: 'od faktów', variant: 'Symulacja · {{name}}', chart: 'Pokaż wykres', compare: 'Porównaj warianty', loan: 'kredyt spłacony', cushion: 'poduszka osiągnięta', cash: 'gotówka na koniec', never: 'nie w tym horyzoncie' },
          palette: { title: 'Dodaj kafelek', hint: 'Podpowiedź', add: 'Dodaj kafelek: {{name}}', categories: { event: { title: 'Zdarzenie', hint: '' }, action: { title: 'Akcja', hint: '' }, control: { title: 'Warunek', hint: '' } } },
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

    const simulate = http.expectOne((r) => r.url === '/api/strategies/simulate-variants' && r.method === 'POST');
    expect(simulate.request.body.nodes.find((n: StrategyNode) => n.id === 'overpay').amount).toBe(5000);
    simulate.flush({ base: result(), variants: [] });
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

  it('zmiana rodzaju akcji na zdarzenie zrywa strzałkę wchodzącą, a zmiana na warunek daje jej wyjściu etykietę', async () => {
    await settle();
    expect(api().incomingTypesOf('overpay')).toEqual(['Income']);

    api().changeType('overpay', 'Trigger');
    expect(api().nodes().find((n) => n.id === 'overpay')?.type).toBe('Trigger');
    expect(api().edges().map((e) => e.id)).toEqual(['e2', 'e3', 'e4', 'e5']);
    expect(api().dirty()).toBe(true);

    api().changeType('payoff', 'Condition');
    expect(api().nodes().find((n) => n.id === 'payoff')?.metric).toBe('CashMinusDebt');
  });

  it('„Zastosuj w budżecie” jest wyłączone bez akcji „do budżetu” albo z niezapisanymi zmianami', async () => {
    const applyButton = (): HTMLButtonElement =>
      [...fixture.nativeElement.querySelectorAll('button')].find((b) => (b as HTMLElement).textContent?.includes('Zastosuj w budżecie')) as HTMLButtonElement;

    await settle();
    expect(applyButton().disabled).toBe(true);

    api().addNode('SetSavingsGoal', { x: 100, y: 200 });
    fixture.detectChanges();
    expect(applyButton().disabled).toBe(true);

    api().discard();
    fixture.detectChanges();
    expect(applyButton().disabled).toBe(true);
  });

  it('z zapisaną akcją „do budżetu” przycisk „Zastosuj w budżecie” jest aktywny', async () => {
    const goal = node({ id: 'goal', type: 'SetSavingsGoal', title: 'Cel', amount: 700 });
    await settle(strategy({ nodes: [...nodes, goal] }));

    const button = [...fixture.nativeElement.querySelectorAll('button')]
      .find((b) => (b as HTMLElement).textContent?.includes('Zastosuj w budżecie')) as HTMLButtonElement;
    expect(button.disabled).toBe(false);
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

  describe('wyjście z niezapisanymi zmianami', () => {
    const modalText = (): string => document.body.textContent!.replace(/\s+/g, ' ');
    const footerButton = (label: string): HTMLButtonElement =>
      [...document.body.querySelectorAll<HTMLButtonElement>('.ant-modal-footer button')].find((b) => b.textContent!.includes(label))!;
    /** Zwraca obietnicę w obiekcie — gołe `return answer` z funkcji async czekałoby na wybór użytkownika. */
    const open = async (): Promise<{ answer: Promise<boolean> }> => {
      const answer = Promise.resolve(api().canLeave());
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
      return { answer };
    };

    it('bez zmian wolno wyjść od razu, bez okna', async () => {
      await settle();

      expect(api().canLeave()).toBe(true);
      expect(api().leaveOpen()).toBe(false);
    });

    it('ze zmianami otwiera okno z alertem i wypisaniem, co się zmieniło, a trasa czeka na wybór', async () => {
      await settle();
      api().addNode('IncreaseSurplus', { x: 100, y: 200 });
      api().onMoveNodes({ nodes: [{ id: 'bonus', position: { x: 5, y: 6 } }] });
      fixture.detectChanges();

      const { answer } = await open();

      expect(api().leaveOpen()).toBe(true);
      expect(modalText()).toContain('Strategia „Kredyt i poduszka” ma niezapisane zmiany.');
      expect(modalText()).toContain('dodane kafelki: 1');
      expect(modalText()).toContain('zmienione kafelki: 1');
      api().stay();
      expect(await answer).toBe(false);
    });

    it('„Zostań na tablicy” nie wypuszcza, a zmiany zostają', async () => {
      await settle();
      api().addNode('IncreaseSurplus', { x: 100, y: 200 });

      const { answer } = await open();
      footerButton('Zostań na tablicy').click();

      expect(await answer).toBe(false);
      expect(api().dirty()).toBe(true);
      expect(api().leaveOpen()).toBe(false);
    });

    it('„Odrzuć i wyjdź” wypuszcza bez żadnego zapisu', async () => {
      await settle();
      api().addNode('IncreaseSurplus', { x: 100, y: 200 });

      const { answer } = await open();
      footerButton('Odrzuć i wyjdź').click();

      expect(await answer).toBe(true);
      expect(http.match((r) => r.method === 'PUT')).toHaveLength(0);
    });

    it('„Zapisz i wyjdź” zapisuje strategię i dopiero potem wypuszcza', async () => {
      await settle();
      api().addNode('IncreaseSurplus', { x: 100, y: 200 });

      const { answer } = await open();
      footerButton('Zapisz i wyjdź').click();
      const put = http.expectOne((r) => r.url === '/api/strategies/s1' && r.method === 'PUT');
      put.flush(strategy({ nodes: put.request.body.nodes }));

      expect(await answer).toBe(true);
      expect(api().dirty()).toBe(false);
    });

    it('nieudany zapis zostawia użytkownika na tablicy — inaczej zmiany przepadłyby po cichu', async () => {
      // Łapie „Zapisz i wyjdź”, które wychodzi mimo błędu zapisu.
      await settle();
      api().addNode('IncreaseSurplus', { x: 100, y: 200 });

      const { answer } = await open();
      footerButton('Zapisz i wyjdź').click();
      http.expectOne((r) => r.url === '/api/strategies/s1' && r.method === 'PUT').flush({}, { status: 500, statusText: 'Server Error' });

      expect(await answer).toBe(false);
      expect(api().dirty()).toBe(true);
    });

    it('zamknięcie karty z niezapisanymi zmianami jest anulowane, bez zmian — nie', async () => {
      await settle();
      const event = { preventDefault: vi.fn() };

      api().onBeforeUnload(event);
      expect(event.preventDefault).not.toHaveBeenCalled();

      api().addNode('IncreaseSurplus', { x: 100, y: 200 });
      api().onBeforeUnload(event);
      expect(event.preventDefault).toHaveBeenCalledTimes(1);
    });

    it('drugie pytanie przed odpowiedzią odrzuca pierwsze jako „zostań”', async () => {
      await settle();
      api().addNode('IncreaseSurplus', { x: 100, y: 200 });

      const first = Promise.resolve(api().canLeave());
      const second = Promise.resolve(api().canLeave());

      expect(await first).toBe(false);
      api().discardAndLeave();
      expect(await second).toBe(true);
    });
  });

  describe('zdarzenia, które nastąpiły', () => {
    const realized = (): StrategyNode => ({ ...nodes.find((n) => n.id === 'bonus')!, actualMonth: '2027-06-01', actualAmount: 8150 });
    const withFact = (): Strategy => strategy({ nodes: nodes.map((n) => (n.id === 'bonus' ? realized() : n)) });

    it('pasek nad tablicą liczy zrealizowane zdarzenia z wszystkich zdarzeń', async () => {
      await settle();
      expect(text()).toContain('Zdarzenia: 0 z 1 zrealizowane');

    });

    it('zdarzenie z faktem pokazuje fakt (miesiąc i kwotę), zielony ✓ i chip „od faktów”', async () => {
      await settle(withFact());

      const bonus = [...fixture.nativeElement.querySelectorAll('.f-node')].find((n) => (n as HTMLElement).textContent!.includes('Premia')) as HTMLElement;
      const glyph = bonus.querySelector('.sb__glyph') as HTMLElement;
      expect(bonus.textContent).toContain('8150');
      expect(bonus.textContent).toContain('czerwiec 2027');
      expect(bonus.textContent).not.toContain('7800');
      expect(glyph.textContent!.trim()).toBe('✓');
      expect(glyph.style.background).toContain('--ds-primary-600');
      expect(text()).toContain('Zdarzenia: 1 z 1 zrealizowane');
      expect(text()).toContain('Symulacja · od faktów');
    });

    it('bez faktów chip nie mówi „od faktów”, a zdarzenie ma ⚡', async () => {
      await settle();

      expect(text()).not.toContain('od faktów');
      const bonus = [...fixture.nativeElement.querySelectorAll('.f-node')].find((n) => (n as HTMLElement).textContent!.includes('Premia')) as HTMLElement;
      expect((bonus.querySelector('.sb__glyph') as HTMLElement).textContent!.trim()).toBe('⚡');
    });

    it('fakt trafia do zapisu razem z planem i jest zmianą do zapisania', async () => {
      await settle();

      api().updateNode(realized());
      fixture.detectChanges();
      expect(api().dirty()).toBe(true);

      const done = api().save();
      const put = http.expectOne((r) => r.url === '/api/strategies/s1' && r.method === 'PUT');
      const sent = put.request.body.nodes.find((n: StrategyNode) => n.id === 'bonus');
      expect([sent.month, sent.amount, sent.actualMonth, sent.actualAmount]).toEqual(['2027-05-01', 7800, '2027-06-01', 8150]);
      put.flush(strategy({ nodes: put.request.body.nodes }));
      await done;
    });
  });

  describe('warianty', () => {
    const chips = (): string[] => [...fixture.nativeElement.querySelectorAll('.svb__chip')].map((c) => (c as HTMLElement).textContent!.trim());
    const offNodes = (): string[] => [...fixture.nativeElement.querySelectorAll('.f-node.sb__node--off')]
      .map((n) => (n as HTMLElement).textContent!.replace(/\s+/g, ' ').trim());

    it('pasek pokazuje wariant bazowy, zapisane warianty i „+ Nowy wariant”', async () => {
      await settle(strategy({ variants: [variant()] }));

      expect(chips()).toEqual(['Bazowy', 'Bez premii', '+ Nowy wariant']);
      expect(api().dirty()).toBe(false);
    });

    it('wybór wariantu wygasza jego wyłączone kafelki, a bazowy ma wszystkie włączone', async () => {
      await settle(strategy({ variants: [variant()] }));
      expect(offNodes()).toEqual([]);

      api().selectVariant('v1');
      fixture.detectChanges();
      expect(offNodes()).toHaveLength(1);
      expect(offNodes()[0]).toContain('Premia');

      api().selectVariant(null);
      fixture.detectChanges();
      expect(offNodes()).toEqual([]);
    });

    it('chip wyniku pokazuje wynik WYBRANEGO wariantu', async () => {
      await settle(strategy({ variants: [variant()] }));
      expect(text()).toContain('kredyt spłacony: lipiec 2027');

      api().selectVariant('v1');
      fixture.detectChanges();

      expect(text()).toContain('Symulacja · Bez premii');
      expect(text()).toContain('kredyt spłacony: sierpień 2027');
    });

    it('„+ Nowy wariant” dodaje wariant bez wyłączeń o wolnej nazwie, wybiera go i oznacza zmianę', async () => {
      await settle(strategy({ variants: [variant({ name: 'Wariant' })] }));

      api().addNewVariant();
      fixture.detectChanges();

      expect(api().variants().map((v) => v.name)).toEqual(['Wariant', 'Wariant 2']);
      expect(api().variants()[1].disabledNodeIds).toEqual([]);
      expect(chips()).toContain('Wariant 2');
      expect(api().dirty()).toBe(true);
    });

    it('wyłączenie kafelka w wariancie jest zmianą do zapisania i trafia do PUT razem z wariantami', async () => {
      await settle(strategy({ variants: [variant({ disabledNodeIds: [] })] }));
      api().selectVariant('v1');

      api().toggleTile({ nodeId: 'overpay', disabled: true });
      fixture.detectChanges();
      expect(api().dirty()).toBe(true);
      expect(offNodes()[0]).toContain('Nadpłać kredyt');

      const done = api().save();
      const put = http.expectOne((r) => r.url === '/api/strategies/s1' && r.method === 'PUT');
      expect(put.request.body.variants).toEqual([{ id: 'v1', name: 'Bez premii', disabledNodeIds: ['overpay'] }]);
      put.flush(strategy({ variants: [variant({ disabledNodeIds: ['overpay'] })] }));
      await done;

      expect(api().dirty()).toBe(false);
    });

    it('usunięcie kafelka zdejmuje go z wyłączeń wariantów, żeby zapis nie dostał 400', async () => {
      // Łapie wariant wyłączający kafelek, którego już nie ma na tablicy: serwer odrzuca taki zapis.
      await settle(strategy({ variants: [variant()] }));

      api().removeNode('bonus');

      expect(api().variants()[0].disabledNodeIds).toEqual([]);
    });

    it('usunięcie wybranego wariantu wraca do bazowego', async () => {
      await settle(strategy({ variants: [variant()] }));
      api().selectVariant('v1');

      api().removeVariant('v1');
      fixture.detectChanges();

      expect(chips()).toEqual(['Bazowy', '+ Nowy wariant']);
      expect(text()).toContain('Symulacja');
      expect(text()).not.toContain('Symulacja · ');
      expect(api().dirty()).toBe(true);
    });

    it('zakładka „Warianty” pokazuje karty z wynikiem każdego wariantu i różnicą względem bazowego', async () => {
      await settle(strategy({ variants: [variant()] }));

      const tab = [...fixture.nativeElement.querySelectorAll('.spt__tab')].find((t) => (t as HTMLElement).textContent!.includes('Warianty')) as HTMLElement;
      tab.click();
      fixture.detectChanges();

      const panel = (fixture.nativeElement.querySelector('app-strategy-variants-panel') as HTMLElement).textContent!.replace(/\s+/g, ' ');
      expect(panel).toContain('Bez premii');
      expect(panel).toContain('kredyt spłacony: sierpień 2027');
      expect(panel).toContain('gotówka na koniec: 14 900 zł');
      expect(panel).toContain('−2400 zł względem bazowego');
    });
  });

  describe('na telefonie', () => {
    const chains = (): string[] => [...fixture.nativeElement.querySelectorAll('.sb__chain')].map((c) => (c as HTMLElement).textContent!.replace(/s+/g, ' '));

    it('domyślnie pokazuje listę kroków od zdarzeń, a nie tablicę', async () => {
      mobile.set(true);
      await settle();

      expect(nodeCount()).toBe(0);
      expect(fixture.nativeElement.querySelector('.sb__mmode')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('.sb__panel')).toBeNull();
      // Kredyt (kafelek bazowy) i Premia otwierają łańcuchy; „Koniec” nie ma strzałki wejściowej, więc jest osobno.
      expect(chains().filter((c) => c.includes('Premia'))[0]).toContain('Nadpłać kredyt');
      expect(chains().filter((c) => c.includes('Premia'))[0]).toMatch(/Warunek.*tak.*Spłać.*nie.*Czekaj/);
      expect(chains().at(-1)).toContain('Bez połączenia');
      expect(chains().at(-1)).toContain('Koniec');
    });

    it('przełączenie na „Tablica” rysuje kafelki, a „Lista kroków” je zdejmuje', async () => {
      mobile.set(true);
      await settle();
      (fixture.componentInstance as unknown as { mode: { set(v: string): void } }).mode.set('board');
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(nodeCount()).toBe(nodes.length);
      expect(fixture.nativeElement.querySelector('.sb__zoom')).not.toBeNull();
    });

    it('dotknięcie kroku otwiera arkusz z ustawieniami kafelka', async () => {
      mobile.set(true);
      await settle();
      const step = [...fixture.nativeElement.querySelectorAll('.sb__step')].find((b) => (b as HTMLElement).textContent!.includes('Nadpłać kredyt')) as HTMLElement;

      step.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(document.body.querySelector('.sb-sheet .snf')).not.toBeNull();
    });

    it('„+ Dodaj krok” dopina nowy kafelek za ostatnim krokiem łańcucha i otwiera jego ustawienia', async () => {
      mobile.set(true);
      await settle();
      const before = api().nodes().length;
      const chain = [...fixture.nativeElement.querySelectorAll('.sb__chain')].find((c) => (c as HTMLElement).textContent!.includes('Premia')) as HTMLElement;

      (chain.querySelector('.sb__add-step') as HTMLElement).click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
      ([...document.body.querySelectorAll('.spal__add')][1] as HTMLElement).click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      const added = api().nodes().at(-1)!;
      expect(api().nodes()).toHaveLength(before + 1);
      // Łańcuch Premii kończy się na „Czekaj” — nowy kafelek ma iść za nim, nie do losowego kafelka.
      expect(api().edges().some((e) => e.from === 'wait' && e.to === added.id)).toBe(true);
      expect(document.body.querySelector('.sb-sheet .snf')).not.toBeNull();
    });

    it('kafelek bazowy (kredyt) nie ma „+ Dodaj krok”, bo nic z niego nie wychodzi', async () => {
      mobile.set(true);
      await settle();
      const loan = [...fixture.nativeElement.querySelectorAll('.sb__chain')].find((c) => (c as HTMLElement).textContent!.includes('Kredyt')) as HTMLElement;

      expect(loan.querySelector('.sb__add-step')).toBeNull();
    });
  });

  it('na desktopie nie ma przełącznika widoków, paska akcji ani listy kroków', async () => {
    await settle();

    expect(fixture.nativeElement.querySelector('.sb__mmode')).toBeNull();
    expect(fixture.nativeElement.querySelector('.sb__bar')).toBeNull();
    expect(fixture.nativeElement.querySelector('.sb__list')).toBeNull();
    expect(fixture.nativeElement.querySelector('.sb__panel')).not.toBeNull();
  });
});
