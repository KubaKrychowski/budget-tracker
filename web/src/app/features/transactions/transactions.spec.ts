import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { NzTableQueryParams } from 'ng-zorro-antd/table';
import { APP_ICONS } from '../../core/icons';
import { CategoryOption } from '../../core/api/models/category-option';
import { TransactionListItem } from '../../core/api/models/transaction-list-item';
import { TransactionListResponse } from '../../core/api/models/transaction-list-response';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../core/confirm-dialog/confirm-dialog-options';
import { Transactions } from './transactions';

/** Atrapa potwierdzenia — test sam decyduje, kiedy i czym „użytkownik" odpowiedział. */
class FakeConfirmDialogService {
  lastOptions: ConfirmDialogOptions | null = null;
  private resolve: ((value: boolean) => void) | null = null;

  confirm(options: ConfirmDialogOptions): Promise<boolean> {
    this.lastOptions = options;
    return new Promise((resolve) => { this.resolve = resolve; });
  }

  respond(confirmed: boolean): void {
    this.resolve?.(confirmed);
    this.resolve = null;
  }
}

/**
 * Lista transakcji. Testujemy dokładnie te miejsca, w których łatwo o CICHY błąd — takie,
 * których nie widać na ekranie, dopóki nie zniszczą danych:
 *
 * - echo inicjalizacyjne `nz-table`, które kasowało filtr z adresu (wejście z dashboardu),
 * - zaznaczenie przeżywające podmianę budżetu (akcja masowa na niewidocznych wierszach),
 * - kwota wyczyszczona w edycji, która wcześniej po cichu stawała się zerem,
 * - zasięg wysyłany do akcji masowych (jawne id kontra „wszystkie pasujące").
 */
describe('Transactions', () => {
  let fixture: ComponentFixture<Transactions>;
  let component: Transactions;
  let http: HttpTestingController;
  let router: Router;
  let confirmDialog: FakeConfirmDialogService;

  interface SelectionPayload {
    ids: string[] | null;
    filter: { budgetIds: string[] | null; direction: string } | null;
  }

  const api = () => component as unknown as {
    items(): TransactionListItem[];
    total(): number;
    summary(): unknown;
    selectedIds(): ReadonlySet<string>;
    selectionCount(): number;
    selectAllMatching(): boolean;
    toggleRow(id: string, checked: boolean): void;
    toggleAllOnPage(checked: boolean): void;
    selectAllMatchingFilter(): void;
    canSelectAllMatching(): boolean;
    editingIds(): ReadonlySet<string>;
    drafts(): Record<string, { description: string; amount: number | null }>;
    updateDraft(id: string, patch: Record<string, unknown>): void;
    editSelected(): void;
    errorFor(id: string): string | null;
    editHasErrors(): boolean;
    saveEdits(): Promise<void>;
    cancelEdit(): void;
    menuRow: { set(v: TransactionListItem): void };
    deleteMenuRow(): void;
    toggleMenuRowLargeExpense(): void;
    bulkDelete(): Promise<void>;
    onQueryParamsChange(params: NzTableQueryParams, table: unknown): void;
    noBudget(): boolean;
    noTransactionsAtAll(): boolean;
    noFilterResults(): boolean;
    crumbs(): { label: string; link?: string; queryParams?: Record<string, string> }[];
    setSearch(value: string): void;
    listError(): unknown;
    selectedBudgetIds(): string[];
    budgetOptions(): { id: string; name: string }[];
    setBudgets(ids: string[]): void;
    loading(): boolean;
  };

  const row = (over: Partial<TransactionListItem> = {}): TransactionListItem => ({
    id: '01a00000-0000-7000-8000-000000000001',
    date: '2026-03-02',
    description: 'BIEDRONKA',
    amount: -100,
    categoryId: 'c1000000-0000-4000-8000-000000000001',
    categoryName: 'Jedzenie',
    status: 'Confirmed',
    isLargeExpense: false,
    confidence: null,
    ...over,
  });

  /** Budżety do multiselecta — odpowiedź listy niesie je razem z wierszami. */
  const budgetOptions = [
    { id: 'b1000000-0000-4000-8000-000000000001', name: 'Podstawowy', month: '2026-03-01', disabled: false },
    { id: 'b1000000-0000-4000-8000-000000000002', name: 'Wariant', month: '2026-02-01', disabled: false },
  ];

  const response = (over: Partial<TransactionListResponse> = {}): TransactionListResponse => ({
    items: [row()],
    total: 1,
    page: 1,
    pageSize: 10,
    summary: {
      totalExpenses: 100,
      totalIncome: 0,
      balance: -100,
      largestExpenseAmount: 100,
    },
    selectedBudgetIds: ['b1000000-0000-4000-8000-000000000001'],
    budgets: budgetOptions,
    hasAnyTransactions: true,
    ...over,
  });

  const categories: CategoryOption[] = [
    { id: 'c1000000-0000-4000-8000-000000000001', name: 'Jedzenie' },
    { id: 'c1000000-0000-4000-8000-000000000002', name: 'Transport' },
  ];

  /** Odpowiada na WSZYSTKIE oczekujące żądania listy — httpResource potrafi wystrzelić kilka. */
  const flushList = (body: TransactionListResponse = response()): void => {
    http.match((r) => r.url === '/api/transactions').forEach((r) => r.flush(body));
  };

  const flushCategories = (): void => {
    http.match('/api/categories').forEach((r) => r.flush(categories));
  };

  /**
   * Domyka cykl: wykrywanie zmian → odpowiedź na żądanie → następne wykrywanie zmian.
   *
   * Pętla, a nie jedno `whenStable`: `httpResource` wystrzeliwuje żądanie DOPIERO w trakcie
   * wykrywania zmian, a nawigacja routera domyka się po kolejnym takcie — więc jedna runda
   * potrafi odsłonić następne żądanie. `whenStable` samo w sobie by tu nie wystarczyło,
   * bo czeka na żądanie, którego nikt jeszcze nie obsłużył.
   */
  const settle = async (body?: TransactionListResponse): Promise<void> => {
    for (let round = 0; round < 6; round++) {
      fixture.detectChanges();
      const pending = http.match((r) => r.url === '/api/transactions' && r.method === 'GET');
      pending.forEach((r) => r.flush(body ?? response()));
      if (pending.length === 0 && round > 1) break;
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  };

  /** Zdarzenie z `nz-table`: komplet stanu, tak jak niesie je prawdziwy `nzQueryParams`. */
  const queryParams = (over: Partial<NzTableQueryParams> = {}): NzTableQueryParams => ({
    pageIndex: 1,
    pageSize: 10,
    sort: [{ key: 'date', value: 'descend' }, { key: 'amount', value: null }],
    filter: [{ key: 'category', value: null }, { key: 'status', value: [] }],
    ...over,
  } as NzTableQueryParams);

  beforeEach(async () => {
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [Transactions],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'transactions', children: [] },
          { path: 'dashboard', children: [] },
        ]),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
        provideNzI18n(pl_PL),
        provideNzDateFnsAdapter(),
        { provide: ConfirmDialogService, useValue: confirmDialog },
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).use('pl');

    router = TestBed.inject(Router);
    await router.navigate(['/transactions'], {
      queryParams: { budgetId: 'b1000000-0000-4000-8000-000000000001' },
    });

    fixture = TestBed.createComponent(Transactions);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
    flushCategories();
    flushList();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  afterEach(() => {
    // `httpResource` porzuca żądanie, gdy parametry zmienią się w locie — to normalna praca
    // sygnałów, nie przeciek. Sprawdzamy więc tylko, czy nie zostało nic NIEOBSŁUŻONEGO.
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
  });

  // ── Echo inicjalizacyjne tabeli ────────────────────────────────────────────────────

  it('ignoruje pierwsze zdarzenie tabeli, żeby nie skasować filtra z adresu', async () => {
    await router.navigate(['/transactions'], {
      queryParams: {
        budgetId: 'b1000000-0000-4000-8000-000000000001',
        categoryId: 'c1000000-0000-4000-8000-000000000001',
      },
    });
    await settle();

    // Tak wygląda echo świeżo zamontowanej tabeli: filtry PUSTE, choć adres je ma.
    const table = {};
    api().onQueryParamsChange(queryParams(), table);
    await fixture.whenStable();

    expect(router.url).toContain('categoryId=c1000000-0000-4000-8000-000000000001');
  });

  it('zapisuje do adresu dopiero KOLEJNE zdarzenia tej samej tabeli', async () => {
    const table = {};
    api().onQueryParamsChange(queryParams(), table); // echo — połknięte
    await fixture.whenStable();

    api().onQueryParamsChange(
      queryParams({ pageIndex: 3, sort: [{ key: 'amount', value: 'ascend' }] }),
      table,
    );
    await settle();

    expect(router.url).toContain('page=3');
    expect(router.url).toContain('sort=amount');
    expect(router.url).toContain('desc=false');
  });

  it('połyka echo KAŻDEGO nowego wcielenia tabeli, nie tylko pierwszego', async () => {
    const first = {};
    api().onQueryParamsChange(queryParams(), first);
    api().onQueryParamsChange(queryParams({ pageIndex: 2 }), first);
    await settle();
    expect(router.url).toContain('page=2');

    // Tabela znika i wraca razem z pustymi stanami — nowa instancja, nowe echo.
    const second = {};
    api().onQueryParamsChange(queryParams({ pageIndex: 1 }), second);
    await fixture.whenStable();

    expect(router.url).toContain('page=2');
  });

  // ── Zaznaczenie ────────────────────────────────────────────────────────────────────

  it('porzuca zaznaczenie i edycję, gdy adres wskaże inny budżet', async () => {
    api().toggleRow(row().id, true);
    api().editSelected();
    expect(api().selectionCount()).toBe(1);
    expect(api().editingIds().size).toBe(1);

    await router.navigate(['/transactions'], {
      queryParams: { budgetId: 'b1000000-0000-4000-8000-000000000099' },
    });
    await settle();

    // Bez tego zostałyby identyfikatory z budżetu, którego użytkownik już nie widzi.
    expect(api().selectionCount()).toBe(0);
    expect(api().editingIds().size).toBe(0);
  });

  it('wysyła filtr razem z jawnymi identyfikatorami, żeby serwer znał budżet', async () => {
    api().toggleRow(row().id, true);
    void api().bulkDelete();
    await fixture.whenStable();
    confirmDialog.respond(true);
    await fixture.whenStable();

    const request = http.expectOne('/api/transactions/bulk-delete');
    const selection = (request.request.body as { selection: SelectionPayload }).selection;

    expect(selection.ids).toEqual([row().id]);
    expect(selection.filter?.budgetIds).toEqual(['b1000000-0000-4000-8000-000000000001']);
    request.flush({ affected: 1 });
    await settle();
  });

  it('przy „wszystkie pasujące" wysyła sam filtr, bez listy identyfikatorów', async () => {
    // Nawigacja WYMUSZA pobranie: `httpResource` odpytuje serwer dopiero przy zmianie
    // parametrów, więc bez tego `settle` z inną odpowiedzią nie miałby czego obsłużyć
    // i test asertowałby na danych z `beforeEach` (jeden wiersz zamiast dwóch).
    await router.navigate(['/transactions'], {
      queryParams: { budgetId: 'b1000000-0000-4000-8000-000000000001', search: 'x' },
    });
    await settle(response({ items: [row(), row({ id: 'x' })], total: 50 }));

    api().toggleAllOnPage(true);
    expect(api().canSelectAllMatching()).toBe(true);
    api().selectAllMatchingFilter();
    expect(api().selectionCount()).toBe(50);

    void api().bulkDelete();
    await fixture.whenStable();
    confirmDialog.respond(true);
    await fixture.whenStable();

    const request = http.expectOne('/api/transactions/bulk-delete');
    const selection = (request.request.body as { selection: SelectionPayload }).selection;

    // Zasięg „wszystkie N pasujących" nie może wysyłać tysięcy GUID-ów.
    expect(selection.ids).toBeNull();
    expect(selection.filter).not.toBeNull();
    request.flush({ affected: 50 });
    await settle();
  });

  it('nie usuwa niczego, gdy użytkownik odrzuci potwierdzenie', async () => {
    api().toggleRow(row().id, true);
    void api().bulkDelete();
    await fixture.whenStable();
    confirmDialog.respond(false);
    await fixture.whenStable();

    http.expectNone('/api/transactions/bulk-delete');
  });

  it('menu wiersza przełącza duży wydatek na przeciwny stan', async () => {
    api().menuRow.set(row({ isLargeExpense: true }));
    api().toggleMenuRowLargeExpense();
    await fixture.whenStable();

    const request = http.expectOne('/api/transactions/bulk-large-expense');
    expect((request.request.body as { isLargeExpense: boolean }).isLargeExpense).toBe(false);
    request.flush({ affected: 1 });
    await settle();
  });

  // ── Walidacja edycji ───────────────────────────────────────────────────────────────

  it('nie zamienia wyczyszczonej kwoty na zero i blokuje zapis', async () => {
    api().toggleRow(row().id, true);
    api().editSelected();

    api().updateDraft(row().id, { amount: null });

    expect(api().drafts()[row().id].amount).toBeNull();
    expect(api().errorFor(row().id)).toBe('amount');
    expect(api().editHasErrors()).toBe(true);

    await api().saveEdits();
    http.expectNone('/api/transactions');
  });

  it('blokuje zapis pustego opisu', async () => {
    api().toggleRow(row().id, true);
    api().editSelected();

    api().updateDraft(row().id, { description: '   ' });

    expect(api().errorFor(row().id)).toBe('description');
    await api().saveEdits();
    http.expectNone('/api/transactions');
  });

  it('blokuje zapis opisu dłuższego niż limit kolumny', async () => {
    api().toggleRow(row().id, true);
    api().editSelected();

    api().updateDraft(row().id, { description: 'x'.repeat(501) });

    expect(api().errorFor(row().id)).toBe('descriptionTooLong');
    await api().saveEdits();
    http.expectNone('/api/transactions');
  });

  it('zapisuje poprawny wiersz razem z budżetem i przyciętym opisem', async () => {
    api().toggleRow(row().id, true);
    api().editSelected();
    api().updateDraft(row().id, { description: '  BIEDRONKA CENTRUM  ', amount: -120 });

    expect(api().editHasErrors()).toBe(false);
    void api().saveEdits();
    await fixture.whenStable();

    const request = http.expectOne(
      (r) => r.url === '/api/transactions' && r.method === 'PATCH',
    );
    const body = request.request.body as {
      budgetIds: string[];
      edits: { description: string; amount: number }[];
    };

    expect(body.budgetIds).toEqual(['b1000000-0000-4000-8000-000000000001']);
    expect(body.edits[0].description).toBe('BIEDRONKA CENTRUM');
    expect(body.edits[0].amount).toBe(-120);

    request.flush([]);
    await settle();
  });

  // ── Puste stany ────────────────────────────────────────────────────────────────────

  it('odróżnia trzy puste stany po polach odpowiedzi', async () => {
    // Każdy stan wymaga ŚWIEŻEJ odpowiedzi, a `httpResource` odpytuje serwer dopiero,
    // gdy zmienią się parametry — stąd inna szukajka przed każdym z trzech przypadków.
    const reload = async (body: TransactionListResponse, marker: string): Promise<void> => {
      await router.navigate(['/transactions'], {
        queryParams: { budgetId: 'b1000000-0000-4000-8000-000000000001', search: marker },
      });
      await settle(body);
    };

    await reload(
      response({ items: [], total: 0, selectedBudgetIds: [], hasAnyTransactions: false }), 'a');
    expect(api().noBudget()).toBe(true);

    await reload(response({ items: [], total: 0, hasAnyTransactions: false }), 'b');
    expect(api().noTransactionsAtAll()).toBe(true);

    await reload(response({ items: [], total: 0, hasAnyTransactions: true }), 'c');
    expect(api().noFilterResults()).toBe(true);
  });

  // ── Breadcrumbs ────────────────────────────────────────────────────────────────────

  it('domyslnie prowadza z dashboardu', () => {
    const labels = api().crumbs().map((c) => c.label);
    expect(labels).toEqual(['dashboard.breadcrumb', 'transactions.title']);
  });

  it('po wejsciu z „Danych treningowych" prowadza z ustawien, nie z dashboardu', async () => {
    // Ten sam ekran ma dwa wejscia. Sztywna sciezka odsylala o dwa ekrany w bok.
    await router.navigate(['/transactions'], {
      queryParams: { budgetId: 'b1000000-0000-4000-8000-000000000001', origin: 'training' },
    });
    await settle();

    const crumbs = api().crumbs();
    expect(crumbs.map((c) => c.label)).toEqual([
      'settings.breadcrumb',
      'settings.tabs.training',
      'transactions.title',
    ]);

    // Srodkowy element MUSI niesc zakladke — inaczej wraca na ustawienia otwarte na budzetach.
    expect(crumbs[1].link).toBe('/settings');
    expect(crumbs[1].queryParams).toEqual({ tab: 'training' });
  });

  it('ostatni element nie jest odnosnikiem, bo to biezacy ekran', () => {
    const crumbs = api().crumbs();
    expect(crumbs[crumbs.length - 1].link).toBeUndefined();
  });

  it('kontekst wejscia przezywa zmiane filtrow', async () => {
    await router.navigate(['/transactions'], {
      queryParams: { budgetId: 'b1000000-0000-4000-8000-000000000001', origin: 'training' },
    });
    await settle();

    api().setSearch('zabka');
    await settle();

    // `changeQuery` scala parametry, wiec `from` zostaje — sciezka nie moze sie zgubic
    // po pierwszym uzyciu wyszukiwarki.
    expect(router.url).toContain('origin=training');
    expect(api().crumbs()[0].label).toBe('settings.breadcrumb');
  });

  // ── Rozmiar strony ─────────────────────────────────────────────────────────────────

  it('zapisuje wybrany rozmiar strony do adresu', async () => {
    const table = {};
    api().onQueryParamsChange(queryParams(), table); // echo
    await fixture.whenStable();

    api().onQueryParamsChange(queryParams({ pageIndex: 1, pageSize: 50 }), table);
    await settle();

    expect(router.url).toContain('pageSize=50');
  });

  it('zmiana rozmiaru strony wraca na pierwszą stronę', async () => {
    // Numer strony po przeliczeniu znaczy co innego niż przed: kto oglądał stronę 8
    // po 10 wierszy, po przełączeniu na 100 wylądowałby na stronie 8 z ośmiuset.
    await router.navigate(['/transactions'], {
      queryParams: { budgetId: 'b1000000-0000-4000-8000-000000000001', page: 8, pageSize: 10 },
    });
    await settle();

    const table = {};
    api().onQueryParamsChange(queryParams({ pageIndex: 8, pageSize: 10 }), table); // echo
    await fixture.whenStable();

    api().onQueryParamsChange(queryParams({ pageIndex: 8, pageSize: 100 }), table);
    await settle();

    expect(router.url).toContain('page=1');
    expect(router.url).toContain('pageSize=100');
  });

  it('zwykła zmiana strony nie resetuje numeru', async () => {
    const table = {};
    api().onQueryParamsChange(queryParams(), table); // echo
    await fixture.whenStable();

    api().onQueryParamsChange(queryParams({ pageIndex: 4, pageSize: 10 }), table);
    await settle();

    expect(router.url).toContain('page=4');
  });

  // ── Stan błędu ───────────────────────────────────────────────────────────────────────

  /**
   * Wymusza NOWE żądanie listy i odpowiada na nie błędem.
   *
   * `beforeEach` zdążył już odpowiedzieć poprawnie, więc bez zmiany parametrów nie ma czego
   * odrzucić — a test, który nic nie odrzuca, przechodzi z niewłaściwego powodu.
   */
  const failWith = async (status: number, body: unknown): Promise<void> => {
    await router.navigate(['/transactions'], { queryParams: { search: 'cokolwiek' } });
    fixture.detectChanges();

    const pending = http.match((r) => r.url === '/api/transactions');
    expect(pending.length).toBeGreaterThan(0);
    pending.forEach((r) => r.flush(body as object, { status, statusText: 'Bad Request' }));

    await fixture.whenStable();
    fixture.detectChanges();
  };

  /**
   * REGRESJA. Odpowiedź 400 (np. `?direction=all` małą literą — wiązanie enuma w minimalnym
   * API jest wrażliwe na wielkość liter) zostawiała kręcący się w nieskończoność spinner
   * nad pustą tabelą.
   *
   * Przyczyna nie była w spinnerze: `ResourceRef.value()` w stanie błędu RZUCA, więc każdy
   * sygnał, który go czytał (`items`, `total`, `summary`, puste stany), wywalał render
   * szablonu i widok zostawał w stanie z poprzedniego przebiegu. Dlatego test sprawdza RENDER,
   * a nie sam sygnał — `loading()` było `false` także przed poprawką.
   */
  it('po błędzie 400 pokazuje komunikat z API zamiast zawieszać render', async () => {
    await failWith(400, { error: 'Zły filtr' });

    expect(fixture.nativeElement.textContent as string).toContain('Zły filtr');
  });

  it('stan błędu nie udaje pustego wyniku', async () => {
    // „Nic nie pasuje do filtra" po nieudanym żądaniu byłoby zdaniem o danych użytkownika,
    // którego nikt nie sprawdził: serwer nie odpowiedział, więc nie wiadomo, co pasuje.
    await failWith(400, { error: 'Zły filtr' });

    expect(api().noBudget()).toBe(false);
    expect(api().noTransactionsAtAll()).toBe(false);
    expect(api().noFilterResults()).toBe(false);
  });

  it('sygnały pochodne przeżywają błąd zasobu', async () => {
    // Sedno poprawki. Bez `valueOf` każdy z nich rzucał — i stąd brał się zawieszony spinner.
    await failWith(400, { error: 'Zły filtr' });

    expect(() => api().items()).not.toThrow();
    expect(() => api().total()).not.toThrow();
    expect(() => api().summary()).not.toThrow();
  });

  // ── Wybór budżetów ───────────────────────────────────────────────────────────────────

  it('wysyła każdy wybrany budżet jako osobne wystąpienie parametru', async () => {
    // Nazwa parametru została w liczbie pojedynczej, żeby stare adresy z jednym budżetem
    // (odnośniki z dashboardu, zakładki) działały dalej bez zmian.
    await router.navigate(['/transactions'], {
      queryParams: { budgetId: ['b1', 'b2'] },
    });
    fixture.detectChanges();

    const request = http.match((r) => r.url === '/api/transactions')[0];
    expect(request.request.params.getAll('budgetId')).toEqual(['b1', 'b2']);

    http.match((r) => r.url === '/api/transactions').forEach((r) => r.flush(response()));
    flushCategories();
    await settle();
  });

  it('pusty wybór pokazuje budżet, który wybrał SERWER', async () => {
    // Inaczej kontrolka świeciłaby pustką przy liście, która jednak jest z konkretnego
    // budżetu — użytkownik nie miałby jak się dowiedzieć, co właściwie ogląda.
    expect(api().selectedBudgetIds()).toEqual(['b1000000-0000-4000-8000-000000000001']);
  });

  it('opcje multiselecta biorą się z odpowiedzi listy, bez osobnego żądania', () => {
    expect(api().budgetOptions().map((b) => b.name)).toEqual(['Podstawowy', 'Wariant']);
    http.expectNone('/api/budgets');
  });

  it('wyczyszczenie wyboru kasuje parametr z adresu', async () => {
    await router.navigate(['/transactions'], { queryParams: { budgetId: ['b1', 'b2'] } });
    await settle();

    api().setBudgets([]);
    await settle();

    expect(router.url).not.toContain('budgetId');
  });

  it('zmiana budżetów porzuca zaznaczenie', async () => {
    // Zaznaczone identyfikatory pochodzą z poprzedniego zestawu — akcja masowa poszłaby
    // na wiersze, których użytkownik już nie widzi.
    api().toggleRow(row().id, true);
    expect(api().selectionCount()).toBe(1);

    await router.navigate(['/transactions'], { queryParams: { budgetId: ['b2'] } });
    await settle();

    expect(api().selectionCount()).toBe(0);
  });
});
