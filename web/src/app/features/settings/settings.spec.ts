import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { APP_ICONS } from '../../core/icons';
import { BudgetListItem } from '../../core/api/models/budget-list-item';
import { Settings } from './settings';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../core/confirm-dialog/confirm-dialog-options';

/**
 * Zamiast prawdziwego `NzModalService` (który otwierałby overlay CDK) — atrapa, która
 * zapamiętuje OSTATNIE wywołanie i pozwala testowi samemu zdecydować, kiedy i czym
 * użytkownik „odpowiedział". To pozwala sprawdzić dwie rzeczy osobno: że `deleteBudget`/
 * `disableBudget` pytają o WŁAŚCIWĄ treść, i że DELETE/POST leci dopiero po potwierdzeniu.
 */
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

/** Stan filtra w panelu (`nzCustomFilter`) — kształt wspólny dla wszystkich sześciu. */
interface FilterHandle {
  visible: { set(v: boolean): void };
  active(): boolean;
  search(): void;
  reset(): void;
}

interface TextFilterHandle extends FilterHandle {
  draft: { set(v: string): void };
}

interface RangeFilterHandle extends FilterHandle {
  draftFrom: { set(v: number | null): void };
  draftTo: { set(v: number | null): void };
}

interface DateRangeFilterHandle extends FilterHandle {
  draftRange: { set(v: [Date, Date] | null): void };
}

/**
 * Ekran „Ustawienia → Budżety". Testujemy te miejsca, w których łatwo o cichy błąd:
 * potwierdzenie usunięcia nazwą, menu wiersza zależne od statusu, alert o przeliczeniu
 * budżetu, okno retencji brane z API (nie z tekstu tłumaczenia), oraz filtry kolumn —
 * wyszukiwarkę tekstową, zakresy „od–do" i to, że zaznaczanie „wszystkie" respektuje
 * aktywny filtr.
 */
describe('Settings', () => {
  let fixture: ComponentFixture<Settings>;
  let component: Settings;
  let http: HttpTestingController;
  let confirmDialog: FakeConfirmDialogService;

  const api = () => component as unknown as {
    menuRow: { set(v: BudgetListItem): void };
    target(): BudgetListItem | null;
    dialog(): string | null;
    menuItems(): { action: string; danger: boolean }[];
    deleteBudget(): Promise<void>;
    disableBudget(): Promise<void>;
    draftInitialBalance: { set(v: number): void };
    initialBalanceChanged(): boolean;
    previewBalance(): number;
    retentionDays(): number;
    openEdit(): void;
    filterByStatus(selected: string[], row: BudgetListItem): boolean;
    sortByStatus(a: BudgetListItem, b: BudgetListItem): number;
    sortByBalance(a: BudgetListItem, b: BudgetListItem): number;
    sortByTransactionCount(a: BudgetListItem, b: BudgetListItem): number;
    filteredBudgets(): BudgetListItem[];
    allSelected(): boolean;
    toggleAll(checked: boolean): void;
    selected(): ReadonlySet<string>;
    nameFilter: TextFilterHandle;
    balanceFilter: RangeFilterHandle;
    initialBalanceFilter: RangeFilterHandle;
    monthlyLimitFilter: RangeFilterHandle;
    transactionCountFilter: RangeFilterHandle;
    createdAtFilter: DateRangeFilterHandle;
  };

  const budget = (over: Partial<BudgetListItem> = {}): BudgetListItem => ({
    id: 'b1000000-0000-4000-8000-000000000001',
    name: 'Podstawowy',
    month: '2026-09-01',
    currency: 'PLN',
    initialBalance: 1000,
    balance: 800,
    monthlyLimit: 0,
    createdAt: '2026-09-01T10:00:00+00:00',
    transactionCount: 2,
    status: 'active',
    disabledAt: null,
    deletedAt: null,
    linkedSavingsBudgetId: null,
    savingsTransferRules: [],
    ...over,
  });

  /** Odpowiedź listy; `retentionDays` celowo inne niż domyślne 30 — patrz test niżej. */
  const flushList = (budgets: BudgetListItem[], retentionDays = 14): void => {
    http.match('/api/budgets').forEach((r) => r.flush({ budgets, retentionDays }));
  };

  beforeEach(async () => {
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [Settings],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'dashboard', children: [] }]),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
        provideNzI18n(pl_PL),
        provideNzDateFnsAdapter(),
        { provide: ConfirmDialogService, useValue: confirmDialog },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      settings: {
        budgets: {
          status: { active: 'Aktywny', disabled: 'Wyłączony', deleted: 'Usunięty' },
          actions: {
            edit: 'Edytuj', disable: 'Wyłącz', enable: 'Włącz',
            reset: 'Zresetuj', delete: 'Usuń', restore: 'Przywróć',
          },
          delete: {
            header: 'Czy na pewno chcesz usunąć budżet „{{name}}"?',
            description: 'Odwracalne przez {{days}} dni.',
            prompt: 'Wprowadź nazwę, aby potwierdzić',
            confirm: 'Usuń',
          },
          disableConfirm: {
            header: 'Czy na pewno chcesz wyłączyć budżet „{{name}}"?',
            description: 'Nie przyjmuje nowych danych, ale zostaje widoczny.',
          },
          columns: { actions: 'Akcje' },
        },
      },
    });
    translate.use('pl');

    fixture = TestBed.createComponent(Settings);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    flushList([
      budget({ id: 'a', name: 'Podstawowy', balance: 800, transactionCount: 2 }),
      budget({ id: 'b', name: 'Testowy', balance: 2500, transactionCount: 30 }),
    ]);
    await fixture.whenStable();
    fixture.detectChanges();
  });

  // ── Lista ──────────────────────────────────────────────────────────────────────────

  it('pokazuje bilans, bilans początkowy i liczbę transakcji', () => {
    const text = (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

    expect(text).toContain('Podstawowy');
    expect(text).toContain('800,00 PLN');
    expect(text).toContain('1 000,00 PLN');
  });

  it('filtruje po statusie', () => {
    expect(api().filterByStatus(['deleted'], budget({ status: 'active' }))).toBe(false);
    expect(api().filterByStatus(['deleted'], budget({ status: 'deleted' }))).toBe(true);
    // Brak zaznaczenia = brak filtra, nie „nic nie pasuje".
    expect(api().filterByStatus([], budget())).toBe(true);
  });

  it('sortuje status w kolejności logicznej, nie alfabetycznej', () => {
    // Alfabetycznie „Usunięty" wypadłby przed „Wyłączony" — logicznie ma być na końcu.
    expect(api().sortByStatus(budget({ status: 'active' }), budget({ status: 'disabled' })))
      .toBeLessThan(0);
    expect(api().sortByStatus(budget({ status: 'disabled' }), budget({ status: 'deleted' })))
      .toBeLessThan(0);
  });

  it('sortuje bilans i liczbę transakcji numerycznie, nie tekstowo', () => {
    const nizszy = budget({ balance: -50, transactionCount: 9 });
    const wyzszy = budget({ balance: 100, transactionCount: 10 });

    expect(api().sortByBalance(nizszy, wyzszy)).toBeLessThan(0);
    // Regresja: porównanie tekstowe dałoby "9" > "10" (leksykograficznie '9' > '1'),
    // choć liczbowo 9 < 10 — liczba transakcji musi się sortować jako liczba.
    expect(api().sortByTransactionCount(nizszy, wyzszy)).toBeLessThan(0);
  });

  // ── Filtr tekstowy: Nazwa ────────────────────────────────────────────────────────────

  it('wyszukuje po fragmencie nazwy, bez uwzględniania wielkości liter i ogonków', () => {
    api().nameFilter.draft.set('podst');
    api().nameFilter.search();

    expect(api().filteredBudgets().map((b) => b.name)).toEqual(['Podstawowy']);
  });

  it('filtr tekstowy działa dopiero po „Szukaj", nie przy każdym wpisanym znaku', () => {
    // Sam `draft` bez `search()` nie ma prawa niczego przefiltrować — to jest to,
    // co odróżnia ten panel od filtrowania „na żywo".
    api().nameFilter.draft.set('testowy');

    expect(api().filteredBudgets().length).toBe(2);
  });

  it('„Wyczyść" zeruje zastosowany filtr, nie tylko pole robocze', () => {
    api().nameFilter.draft.set('podst');
    api().nameFilter.search();
    expect(api().filteredBudgets().length).toBe(1);

    api().nameFilter.reset();
    expect(api().filteredBudgets().length).toBe(2);
    expect(api().nameFilter.active()).toBe(false);
  });

  // ── Filtr zakresowy: kwoty i liczba transakcji ──────────────────────────────────────

  it('filtruje zakresem „od–do", z otwartym końcem gdy pole puste', () => {
    api().balanceFilter.draftFrom.set(1000);
    api().balanceFilter.draftTo.set(null); // bez górnej granicy
    api().balanceFilter.search();

    expect(api().filteredBudgets().map((b) => b.name)).toEqual(['Testowy']);
  });

  it('granice zakresu są WŁĄCZNIE, nie ostro', () => {
    api().balanceFilter.draftFrom.set(800);
    api().balanceFilter.draftTo.set(800);
    api().balanceFilter.search();

    expect(api().filteredBudgets().map((b) => b.name)).toEqual(['Podstawowy']);
  });

  // ── Filtr zakresowy na datach ────────────────────────────────────────────────────────

  it('filtr daty porównuje po DNIU w czasie lokalnym, nie po pełnym znaczniku czasu', () => {
    // Oba budżety mają createdAt „2026-09-01T10:00:00+00:00" — zakres obejmujący ten
    // dzień musi je złapać, mimo że porównanie po pełnym Date.getTime() by ich nie dopasowało.
    api().createdAtFilter.draftRange.set([new Date(2026, 8, 1), new Date(2026, 8, 1)]);
    api().createdAtFilter.search();

    expect(api().filteredBudgets().length).toBe(2);

    api().createdAtFilter.reset();
    api().createdAtFilter.draftRange.set([new Date(2026, 8, 2), new Date(2026, 8, 30)]);
    api().createdAtFilter.search();

    expect(api().filteredBudgets().length).toBe(0);
  });

  // ── Łączenie filtrów i zaznaczanie ───────────────────────────────────────────────────

  it('łączy kilka aktywnych filtrów przez AND', () => {
    api().nameFilter.draft.set('testowy');
    api().nameFilter.search();
    api().balanceFilter.draftFrom.set(10_000); // „Testowy" ma 2500 — poza zakresem
    api().balanceFilter.search();

    expect(api().filteredBudgets()).toEqual([]);
  });

  it('„zaznacz wszystkie" zaznacza tylko to, co przeszło przez filtr', () => {
    api().nameFilter.draft.set('podst');
    api().nameFilter.search();

    api().toggleAll(true);

    expect(api().selected().size).toBe(1);
    expect(api().allSelected()).toBe(true);

    // Zdjęcie filtra odsłania drugi wiersz, który NIE był zaznaczony — „zaznacz wszystkie"
    // przy aktywnym filtrze nie mogło go po cichu też zaznaczyć.
    api().nameFilter.reset();
    expect(api().allSelected()).toBe(false);
  });

  // ── Usunięcie i wyłączenie (przez ConfirmDialogService) ─────────────────────────────

  it('bierze okno przywracania z API, a nie z tekstu tłumaczenia', () => {
    // Gdyby liczba dni siedziała w tłumaczeniu, zmiana ustawienia po stronie serwera
    // zamieniłaby komunikat modala w nieprawdę.
    expect(api().retentionDays()).toBe(14);
  });

  it('pyta o nazwę budżetu w pytaniu i podaje ją jako klucz potwierdzenia', () => {
    const row = budget();
    api().menuRow.set(row);

    void api().deleteBudget();

    expect(confirmDialog.lastOptions?.header).toContain(row.name);
    // Klucz to DOKŁADNIE nazwa budżetu — usunięcie zabiera ze sobą wszystkie jego
    // transakcje, więc samo kliknięcie nie wystarcza.
    expect(confirmDialog.lastOptions?.confirmKey).toBe(row.name);
  });

  it('nie wysyła DELETE, dopóki confirm nie rozstrzygnie na tak', async () => {
    const row = budget();
    api().menuRow.set(row);

    const done = api().deleteBudget();
    http.expectNone({ method: 'DELETE', url: `/api/budgets/${row.id}` });

    confirmDialog.respond(false);
    await done;
    http.expectNone({ method: 'DELETE', url: `/api/budgets/${row.id}` });
  });

  it('wysyła DELETE dopiero po potwierdzeniu w dialogu', async () => {
    const row = budget();
    api().menuRow.set(row);

    const done = api().deleteBudget();
    confirmDialog.respond(true);
    await Promise.resolve(); // odczekaj mikrotaski między rozwiązaniem Promise a wywołaniem HTTP

    // Kasowanie idzie metodą DELETE na adres z BusinessId — nie POST-em na cokolwiek innego.
    http.expectOne({ method: 'DELETE', url: `/api/budgets/${row.id}` }).flush(null);
    await done;
  });

  it('wyłączenie pyta o potwierdzenie, ale BEZ klucza do przepisania', () => {
    // Wyłączenie jest odwracalne jednym kliknięciem („Włącz") — pytanie o nazwę
    // byłoby tarciem nieproporcjonalnym do skutków.
    const row = budget();
    api().menuRow.set(row);

    void api().disableBudget();

    expect(confirmDialog.lastOptions?.header).toContain(row.name);
    expect(confirmDialog.lastOptions?.confirmKey).toBeUndefined();
  });

  it('wysyła POST /disable dopiero po potwierdzeniu', async () => {
    const row = budget();
    api().menuRow.set(row);

    const done = api().disableBudget();
    confirmDialog.respond(true);
    await Promise.resolve();

    http.expectOne({ method: 'POST', url: `/api/budgets/${row.id}/disable` }).flush(null);
    await done;
  });

  // ── Edycja ─────────────────────────────────────────────────────────────────────────

  it('ostrzega o przeliczeniu dopiero po zmianie bilansu początkowego', () => {
    api().menuRow.set(budget());
    api().openEdit();

    // Alert widoczny od otwarcia modala nauczyłby użytkownika go ignorować.
    expect(api().initialBalanceChanged()).toBe(false);

    api().draftInitialBalance.set(2000);
    expect(api().initialBalanceChanged()).toBe(true);
  });

  it('przesuwa podgląd bilansu razem z bilansem początkowym', () => {
    api().menuRow.set(budget({ initialBalance: 1000, balance: 800 }));
    api().openEdit();

    api().draftInitialBalance.set(2000);

    // Bilans bieżący to bilans początkowy + suma transakcji (-200), więc przesuwa się
    // o dokładnie tyle, o ile przesunął się punkt startu.
    expect(api().previewBalance()).toBe(1800);
  });

  // ── Menu wiersza ───────────────────────────────────────────────────────────────────

  it('daje usuniętemu budżetowi tylko przywracanie', () => {
    api().menuRow.set(budget({ status: 'deleted', deletedAt: '2026-09-03T12:00:00+00:00' }));

    // Edycja, reset i ponowne usunięcie nie mają sensu dla czegoś, czego już nie ma.
    expect(api().menuItems().map((i) => i.action)).toEqual(['restore']);
  });

  it('zamienia „Wyłącz" na „Włącz" dla wyłączonego budżetu', () => {
    api().menuRow.set(budget({ status: 'disabled', disabledAt: '2026-09-03T12:00:00+00:00' }));

    const actions = api().menuItems().map((i) => i.action);

    expect(actions).toContain('enable');
    expect(actions).not.toContain('disable');
    // Wyłączony budżet dalej daje się edytować, resetować i usuwać — blokada dotyczy
    // wyłącznie dopisywania nowych danych.
    expect(actions).toEqual(['edit', 'savingsLink', 'enable', 'reset', 'delete']);
  });
});
