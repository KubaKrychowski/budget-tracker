import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { APP_ICONS } from '../../core/icons';
import { EpisodicOrderRow, EpisodicOrdersResponse } from '../../core/api/models/episodic-orders';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../core/confirm-dialog/confirm-dialog-options';
import { EpisodicOrders } from './episodic-orders';

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

/** Dostęp do chronionych członków — testy sterują modalami i akcjami tak jak szablon. */
interface EpisodicOrdersApi {
  openEditor(row: EpisodicOrderRow | null): void;
  switchKind(kind: 'planned' | 'realized'): void;
  draftName: { set(v: string): void };
  draftDescription: { set(v: string): void };
  draftCategoryId: { set(v: string | null): void };
  draftAmount: { set(v: number | null): void };
  draftDueMonth: { set(v: Date | null): void };
  draftTransactionId: { set(v: string | null): void };
  canSave(): boolean;
  save(): Promise<void>;
  createReservation(row: EpisodicOrderRow): Promise<void>;
  openPurchase(row: EpisodicOrderRow): void;
  purchaseTransactionId: { set(v: string | null): void };
  confirmPurchase(): Promise<void>;
  undoPurchase(row: EpisodicOrderRow): Promise<void>;
  remove(row: EpisodicOrderRow): Promise<void>;
  switchTab(tab: 'planned' | 'realized'): void;
}

/** Ekran „Zlecenia epizodyczne”. Uzbierane i kandydatów liczy serwer — tu sprawdzamy, co ekran mówi i wysyła. */
describe('EpisodicOrders', () => {
  let fixture: ComponentFixture<EpisodicOrders>;
  let http: HttpTestingController;
  let confirmDialog: FakeConfirmDialogService;

  const planned = (over: Partial<EpisodicOrderRow> = {}): EpisodicOrderRow => ({
    id: 'e1',
    name: 'Nowy laptop',
    description: 'do pracy',
    categoryId: 'c1',
    categoryName: 'Elektronika',
    amount: 4000,
    dueMonth: '2027-03-01',
    date: null,
    transactionId: null,
    transactionDescription: null,
    wasPlanned: true,
    reservationId: 'r1',
    collected: 1800,
    ...over,
  });

  const realized = (over: Partial<EpisodicOrderRow> = {}): EpisodicOrderRow => planned({
    id: 'e2', name: 'Serwis auta', description: null, categoryId: null, categoryName: 'Samochód', amount: 1200,
    dueMonth: null, date: '2026-08-20', transactionId: 't1', transactionDescription: 'WARSZTAT', wasPlanned: false,
    reservationId: null, collected: null, ...over,
  });

  const response = (over: Partial<EpisodicOrdersResponse> = {}): EpisodicOrdersResponse => ({
    planned: [planned(), planned({ id: 'e3', name: 'Opony zimowe', reservationId: null, collected: null, amount: 1400 })],
    realized: [realized()],
    plannedTotal: 5400,
    collectedTotal: 1800,
    reservedTotal: 4000,
    realizedThisYear: 1200,
    withoutSavingsCount: 1,
    currentMonth: '2026-09-01',
    selectedBudgetIds: ['b1'],
    budgets: [{ id: 'b1', name: 'Domowy', month: '2026-09-01', disabled: false }],
    ...over,
  });

  const api = (): EpisodicOrdersApi => fixture.componentInstance as unknown as EpisodicOrdersApi;
  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  const settle = async (body: EpisodicOrdersResponse = response()): Promise<void> => {
    fixture.detectChanges();
    http.match((r) => r.url === '/api/episodic-orders').forEach((r) => r.flush(body));
    http.match((r) => r.url === '/api/categories').forEach((r) => r.flush([{ id: 'c1', name: 'Elektronika' }]));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [EpisodicOrders],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'dashboard', children: [] },
          { path: 'episodic-orders', children: [] },
          { path: 'transactions', children: [] },
          { path: 'savings/reservations', children: [] },
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
      episodicOrders: {
        remove: 'Usuń',
        banner: {
          planned: { few: '{{count}} zaplanowane zakupy na {{amount}} zł.' },
          withoutSavings: 'Bez oszczędzania: {{count}}.',
        },
        tabs: { planned: 'Zaplanowane ({{count}})', realized: 'Zrealizowane ({{count}})' },
        table: { noSavings: '— brak', noDue: 'bez terminu' },
        source: { reservation: 'z rezerwacji', unplanned: 'bez planu' },
        removeConfirm: { header: 'Usunąć zlecenie „{{name}}”?', withReservation: 'Zniknie też rezerwacja.' },
      },
    });
    translate.use('pl');

    await TestBed.inject(Router).navigate(['/episodic-orders']);
    fixture = TestBed.createComponent(EpisodicOrders);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
  });

  it('zaplanowane: opis pod nazwą, termin, postęp rezerwacji albo „brak”, a baner liczy zakupy bez oszczędzania', async () => {
    await settle();

    expect(text()).toContain('2 zaplanowane zakupy na 5400 zł. Bez oszczędzania: 1.');
    expect(text()).toContain('do pracy');
    expect(text()).toContain('03.2027');
    expect(text()).toContain('1800 z 4000 zł');
    expect(text()).toContain('— brak');
    expect(text()).not.toContain('WARSZTAT');
  });

  it('zakładka „Zrealizowane” jedzie w adresie i pokazuje transakcję ze źródłem', async () => {
    await settle();

    api().switchTab('realized');
    await fixture.whenStable();
    await settle();

    expect(TestBed.inject(Router).url).toContain('tab=realized');
    expect(text()).toContain('20.08.2026');
    expect(text()).toContain('WARSZTAT · bez planu');
  });

  it('zaplanowane wysyła plan z miesiącem terminu, a bez kategorii nie da się zapisać', async () => {
    await settle();
    api().openEditor(null);
    api().draftName.set(' Opony ');
    api().draftAmount.set(1400);
    api().draftDueMonth.set(new Date(2026, 10, 17));
    expect(api().canSave()).toBe(false);

    api().draftCategoryId.set('c1');
    expect(api().canSave()).toBe(true);

    const saving = api().save();
    const request = http.expectOne((r) => r.method === 'POST' && r.url === '/api/episodic-orders');
    expect(request.request.body).toEqual({
      budgetId: 'b1', name: 'Opony', description: null, transactionId: null,
      categoryId: 'c1', amount: 1400, dueMonth: '2026-11-01',
    });
    request.flush({ id: 'e9' });
    await saving;
  });

  it('„Bez terminu” pozwala zapisać zakup bez daty i wysyła pusty termin', async () => {
    await settle();
    const component = api() as EpisodicOrdersApi & { draftNoDue: { set(v: boolean): void } };
    component.openEditor(null);
    component.draftName.set('Rower');
    component.draftCategoryId.set('c1');
    component.draftAmount.set(2000);
    component.draftDueMonth.set(new Date(2026, 10, 1));
    component.draftNoDue.set(true);
    expect(component.canSave()).toBe(true);

    const saving = component.save();
    const request = http.expectOne((r) => r.method === 'POST' && r.url === '/api/episodic-orders');
    expect(request.request.body).toMatchObject({ name: 'Rower', dueMonth: null, amount: 2000 });
    request.flush({ id: 'e9' });
    await saving;

    await settle(response({ planned: [planned({ name: 'Rower', dueMonth: null })] }));
    expect(text()).toContain('bez terminu');
  });

  it('„Już zrealizowane” szuka wydatków na serwerze i wysyła transakcję bez planu', async () => {
    await settle();
    vi.useFakeTimers();
    try {
      api().openEditor(null);
      api().switchKind('realized');
      vi.advanceTimersByTime(400);
      const candidates = http.expectOne((r) => r.url === '/api/episodic-orders/candidates');
      expect(candidates.request.params.get('budgetId')).toBe('b1');
      candidates.flush([{ id: 't9', date: '2026-08-20', description: 'WARSZTAT', amount: -1200, categoryName: 'Samochód' }]);
    } finally {
      vi.useRealTimers();
    }

    api().draftName.set('Serwis auta');
    expect(api().canSave()).toBe(false);
    api().draftTransactionId.set('t9');

    const saving = api().save();
    const request = http.expectOne((r) => r.method === 'POST' && r.url === '/api/episodic-orders');
    expect(request.request.body).toEqual({
      budgetId: 'b1', name: 'Serwis auta', description: null, transactionId: 't9',
      categoryId: null, amount: null, dueMonth: null,
    });
    request.flush({ id: 'e9' });
    await saving;
  });

  it('zmiana zrealizowanego wysyła tylko nazwę i opis', async () => {
    await settle();
    api().openEditor(realized());
    api().draftDescription.set('rozrząd');
    expect(api().canSave()).toBe(true);

    const saving = api().save();
    const request = http.expectOne((r) => r.method === 'PUT' && r.url === '/api/episodic-orders/e2');
    expect(request.request.body).toMatchObject({ name: 'Serwis auta', description: 'rozrząd', transactionId: null });
    request.flush(null);
    await saving;
  });

  it('„Oznacz jako kupione” pyta o kandydatów zlecenia i wysyła wybraną transakcję', async () => {
    await settle();

    api().openPurchase(planned());
    const candidates = http.expectOne((r) => r.url === '/api/episodic-orders/candidates');
    expect(candidates.request.params.get('orderId')).toBe('e1');
    candidates.flush([{ id: 't5', date: '2026-10-30', description: 'SKLEP', amount: -4200, categoryName: null }]);
    await fixture.whenStable();

    api().purchaseTransactionId.set('t5');
    const purchasing = api().confirmPurchase();
    const request = http.expectOne((r) => r.method === 'PUT' && r.url === '/api/episodic-orders/e1/purchase');
    expect(request.request.body).toEqual({ transactionId: 't5' });
    request.flush(null);
    await purchasing;
  });

  it('„Załóż cel oszczędzania” i „Cofnij do zaplanowanych” to jedno żądanie bez dialogu', async () => {
    await settle();

    const reserving = api().createReservation(planned({ reservationId: null }));
    http.expectOne((r) => r.method === 'POST' && r.url === '/api/episodic-orders/e1/reservation').flush({ id: 'r9' });
    await reserving;

    const undoing = api().undoPurchase(realized({ wasPlanned: true }));
    http.expectOne((r) => r.method === 'DELETE' && r.url === '/api/episodic-orders/e2/purchase').flush(null);
    await undoing;
  });

  it('usunięcie pyta czerwonym przyciskiem i uprzedza o rezerwacji', async () => {
    await settle();

    const removing = api().remove(planned());
    await Promise.resolve();
    expect(confirmDialog.lastOptions?.danger).toBe(true);
    expect(confirmDialog.lastOptions?.description).toBe('Zniknie też rezerwacja.');
    http.expectNone((r) => r.method === 'DELETE');

    confirmDialog.respond(true);
    await Promise.resolve();
    await Promise.resolve();
    http.expectOne((r) => r.method === 'DELETE' && r.url === '/api/episodic-orders/e1').flush(null);
    await removing;
  });
});
