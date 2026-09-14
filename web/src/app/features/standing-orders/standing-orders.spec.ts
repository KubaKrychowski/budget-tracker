import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { APP_ICONS } from '../../core/icons';
import { ActiveBudget } from '../../core/active-budget';
import { StandingOrderPin, StandingOrderRow, StandingOrdersResponse } from '../../core/api/models/standing-orders';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../core/confirm-dialog/confirm-dialog-options';
import { StandingOrders } from './standing-orders';

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

/** Dostęp do chronionych członków — testy sterują modalem i akcjami tak jak szablon. */
interface StandingOrdersApi {
  openEditor(row: StandingOrderRow | null): void;
  draftName: { set(v: string): void };
  draftAmount: { set(v: number | null): void };
  draftRhythm: { set(v: string): void };
  draftDueMonth: { set(v: number | null): void };
  draftPattern: { set(v: string): void };
  draftFrom: { set(v: number | null): void };
  draftTo: { set(v: number | null): void };
  canSave(): boolean;
  save(): Promise<void>;
  remove(row: StandingOrderRow): Promise<void>;
  unpin(pin: StandingOrderPin): Promise<void>;
  goToLinked(row: StandingOrderRow): void;
}

/** Ekran „Zlecenia stałe”. Stan w miesiącu i dopasowania liczy serwer — tu sprawdzamy, co ekran mówi i wysyła. */
describe('StandingOrders', () => {
  let fixture: ComponentFixture<StandingOrders>;
  let http: HttpTestingController;
  let confirmDialog: FakeConfirmDialogService;

  const row = (over: Partial<StandingOrderRow> = {}): StandingOrderRow => ({
    id: 'o1',
    name: 'Czynsz',
    expectedAmount: 2200,
    rhythm: 'Monthly',
    dueMonth: null,
    titlePattern: 'czynsz',
    amountFrom: 2000,
    amountTo: 2500,
    categoryName: 'Mieszkanie',
    state: 'Paid',
    paidOn: '2026-09-05',
    paidAmount: 2200,
    usualDay: 5,
    linkedCount: 12,
    ...over,
  });

  const response = (over: Partial<StandingOrdersResponse> = {}): StandingOrdersResponse => ({
    month: '2026-09-01',
    currentMonth: '2026-09-01',
    orders: [row()],
    recent: [{ transactionId: 't1', standingOrderId: 'o1', standingOrderName: 'Czynsz', date: '2026-09-05', amount: 2200, differentAmount: false }],
    monthlyTotal: 2200,
    dueCount: 1,
    paidCount: 1,
    waitingAmount: 0,
    differentAmountCount: 0,
    selectedBudgetIds: ['b1'],
    budgets: [{ id: 'b1', name: 'Domowy', month: '2026-09-01', disabled: false }],
    ...over,
  });

  const api = (): StandingOrdersApi => fixture.componentInstance as unknown as StandingOrdersApi;
  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  const settle = async (body: StandingOrdersResponse = response()): Promise<void> => {
    fixture.detectChanges();
    http.match((r) => r.url === '/api/standing-orders').forEach((r) => r.flush(body));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [StandingOrders],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'dashboard', children: [] },
          { path: 'standing-orders', children: [] },
          { path: 'transactions', children: [] },
        ]),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
        provideNzI18n(pl_PL),
        { provide: ConfirmDialogService, useValue: confirmDialog },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      dashboard: { configuration: 'Konfiguracja' },
      standingOrders: {
        remove: 'Usuń',
        goToLinked: 'Przejdź do powiązanych ({{count}})',
        banner: {
          empty: 'Nie masz jeszcze zleceń stałych.',
          waiting: 'W tym miesiącu czekają jeszcze: {{list}}.',
          missed: '{{month}} — nie zeszło: {{list}}.',
          different: 'Inna kwota niż zwykle: {{list}}.',
        },
        table: { rule: 'tytuł zawiera „{{pattern}}” · {{from}}–{{to}} zł' },
        state: {
          paid: 'Zeszło {{date}}',
          paidDifferent: 'Zeszło {{date}} · {{amount}} zł (inna kwota)',
          waitingUsual: 'Czeka · zwykle do {{day}}.',
          notDue: 'Nie w tym miesiącu',
        },
        editor: {
          preview: 'Reguła pasuje do {{count}} transakcji — ostatnia {{date}}, {{amount}} zł.',
          previewTaken: '{{count}} z nich należy już do innego zlecenia.',
        },
        removeConfirm: { header: 'Usunąć zlecenie „{{name}}”?', description: 'Przypiętych transakcji: {{count}}.' },
      },
    });
    translate.use('pl');

    await TestBed.inject(Router).navigate(['/standing-orders']);
    fixture = TestBed.createComponent(StandingOrders);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
  });

  it('pyta o JEDEN budżet z widoku, gdy adres go nie podaje', async () => {
    TestBed.inject(ActiveBudget).set(['z-dashboardu']);
    fixture.detectChanges();

    const [request] = http.match((r) => r.url === '/api/standing-orders');
    expect(request.request.params.getAll('budgetId')).toEqual(['z-dashboardu']);
    request.flush(response());
    await fixture.whenStable();
  });

  it('pokazuje regułę pod nazwą, kategorię przypiętych transakcji i stan w miesiącu', async () => {
    await settle(response({
      orders: [
        row(),
        row({ id: 'o2', name: 'Kredyt', state: 'Waiting', paidOn: null, paidAmount: null, usualDay: 15, categoryName: null }),
        row({ id: 'o3', name: 'OC', rhythm: 'Yearly', dueMonth: 5, state: 'NotDue', paidOn: null, paidAmount: null }),
      ],
    }));

    expect(text()).toContain('tytuł zawiera „czynsz” · 2000,00–2500,00 zł');
    expect(text()).toContain('Mieszkanie');
    expect(text()).toContain('Zeszło 5.09');
    expect(text()).toContain('Czeka · zwykle do 15.');
    expect(text()).toContain('Nie w tym miesiącu');
  });

  it('baner wymienia czekające zlecenia, a inna kwota idzie opisem', async () => {
    await settle(response({
      orders: [
        row({ id: 'o2', name: 'Kredyt', expectedAmount: 653, state: 'Waiting', paidOn: null, paidAmount: null }),
        row({ state: 'PaidDifferentAmount', paidAmount: 2350 }),
      ],
    }));

    const banner = fixture.nativeElement.querySelector('.so__banner') as HTMLElement;
    expect(banner.textContent).toContain('W tym miesiącu czekają jeszcze: Kredyt (653,00 zł).');
    expect(banner.textContent).toContain('Inna kwota niż zwykle: Czynsz (2350,00 zł).');
  });

  it('bez niczego do powiedzenia nie pokazuje baneru', async () => {
    await settle();
    expect(fixture.nativeElement.querySelector('.so__banner')).toBeNull();
  });

  it('„Przejdź do powiązanych” otwiera listę transakcji z filtrem zlecenia i okruszkiem powrotu', async () => {
    await settle();

    api().goToLinked(row());
    await new Promise((resolve) => setTimeout(resolve));

    const router = TestBed.inject(Router);
    const url = router.parseUrl(router.url);
    expect(url.root.children['primary']?.segments.map((s) => s.path)).toEqual(['transactions']);
    expect(url.queryParamMap.get('standingOrderId')).toBe('o1');
    expect(url.queryParamMap.get('origin')).toBe('standing-orders');
    expect(url.queryParamMap.get('budgetId')).toBe('b1');
  });

  it('podgląd reguły pyta serwer dopiero przy poprawnej regule i pokazuje wynik', async () => {
    // Sztuczne zegary DOPIERO po wczytaniu — `whenStable()` na sztucznym zegarze nigdy by się nie skończyło.
    await settle();
    vi.useFakeTimers();
    try {
      api().openEditor(null);
      api().draftPattern.set('cz');
      api().draftFrom.set(2000);
      api().draftTo.set(2500);
      fixture.detectChanges();
      vi.advanceTimersByTime(400);
      http.expectNone((r) => r.url === '/api/standing-orders/preview');

      api().draftPattern.set('czynsz');
      fixture.detectChanges();
      vi.advanceTimersByTime(400);

      const preview = http.expectOne((r) => r.url === '/api/standing-orders/preview');
      expect(preview.request.body).toEqual({
        budgetId: 'b1', standingOrderId: null, titlePattern: 'czynsz', amountFrom: 2000, amountTo: 2500,
      });
      preview.flush({ matchCount: 12, takenByOtherOrders: 1, lastDate: '2026-09-05', lastAmount: 2350 });
    } finally {
      vi.useRealTimers();
    }
  });

  it('zapis wysyła regułę, a roczne bez miesiąca nie da się zapisać', async () => {
    await settle();
    api().openEditor(null);
    api().draftName.set('OC samochodu');
    api().draftAmount.set(1200);
    api().draftRhythm.set('Yearly');
    api().draftPattern.set('polisa oc');
    api().draftFrom.set(1100);
    api().draftTo.set(1400);
    expect(api().canSave()).toBe(false);

    api().draftDueMonth.set(5);
    expect(api().canSave()).toBe(true);

    const saving = api().save();
    const request = http.expectOne((r) => r.method === 'POST' && r.url === '/api/standing-orders');
    expect(request.request.body).toEqual({
      budgetId: 'b1', name: 'OC samochodu', expectedAmount: 1200, rhythm: 'Yearly', dueMonth: 5,
      titlePattern: 'polisa oc', amountFrom: 1100, amountTo: 1400,
    });
    request.flush({ id: 'o9', linkedCount: 3 });
    await saving;
  });

  it('usunięcie pyta czerwonym przyciskiem i mówi, ile transakcji straci przypięcie', async () => {
    await settle();

    const removing = api().remove(row());
    await Promise.resolve();
    expect(confirmDialog.lastOptions?.danger).toBe(true);
    expect(confirmDialog.lastOptions?.description).toBe('Przypiętych transakcji: 12.');
    http.expectNone((r) => r.method === 'DELETE');

    confirmDialog.respond(true);
    await Promise.resolve();
    await Promise.resolve();
    http.expectOne((r) => r.method === 'DELETE' && r.url === '/api/standing-orders/o1').flush(null);
    await removing;
  });

  it('„Odepnij” adresuje transakcję, nie zlecenie', async () => {
    await settle();

    const unpinning = api().unpin(response().recent[0]);
    http.expectOne((r) => r.method === 'DELETE' && r.url === '/api/standing-orders/pins/t1').flush(null);
    await unpinning;
  });
});
