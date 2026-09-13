import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { APP_ICONS } from '../../core/icons';
import {
  ReservationsResponse, SavingsReservation, SettleCandidate,
} from '../../core/api/models/reservations';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../core/confirm-dialog/confirm-dialog-options';
import { By } from '@angular/platform-browser';
import { ActiveBudget } from '../../core/active-budget';
import { BudgetSwitcher } from '../../core/budget-switcher/budget-switcher';
import { Reservations } from './reservations';

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
 * Ekran „Wszystkie rezerwacje".
 *
 * ⚠️ Te testy patrzą na DOM, nie na sygnały. Poprzednim razem na tym ekranie-siostrze
 * (podgląd importu) ta sama reguła siedziała w trzech kopiach — w logice, w szablonie
 * i w licznikach — a testy logiki przeszły, gdy widok pokazywał co innego. Jeśli reguła
 * ma dojść do użytkownika, to musi ją potwierdzić tekst na ekranie.
 */
describe('Reservations', () => {
  let fixture: ComponentFixture<Reservations>;
  let http: HttpTestingController;
  let confirmDialog: FakeConfirmDialogService;

  const row = (over: Partial<SavingsReservation> = {}): SavingsReservation => ({
    id: 'r1',
    name: 'Ubezpieczenie OC',
    amount: 1800,
    dueMonth: '2026-05-01',
    collected: 1800,
    status: 'Collecting',
    settledOn: null,
    settledTransactionId: null,
    ...over,
  });

  const response = (over: Partial<ReservationsResponse> = {}): ReservationsResponse => ({
    reservations: [row()],
    accountBalance: 9400,
    reservedTotal: 5000,
    settledTotal: 1800,
    collectedTotal: 1800,
    freeFunds: 4400,
    coveredBy: null,
    selectedBudgetIds: ['b1'],
    budgets: [
      { id: 'b1', name: 'Budżet domowy', month: '2026-11-01', disabled: false },
      { id: 'b2', name: 'Wariant', month: '2026-10-01', disabled: true },
    ],
    ...over,
  });

  const text = (): string =>
    (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  /** Tekst całego dokumentu — modale NG-ZORRO renderują się poza hostem komponentu. */
  const documentText = (): string =>
    (document.body.textContent as string).replace(/\s+/g, ' ');

  const settleList = async (body: ReservationsResponse = response()): Promise<void> => {
    fixture.detectChanges();
    // Dopasowanie po SAMEJ ścieżce: `match(string)` porównuje adres RAZEM z parametrami,
    // a ekran woła `/api/savings/reservations?budgetId=b1`. Niedopasowane żądanie zostaje
    // wiszącym zadaniem i `whenStable()` nigdy się nie kończy — test wygląda wtedy na timeout,
    // a nie na pomyłkę w dopasowaniu.
    http.match((r) => r.url === '/api/savings/reservations').forEach((r) => r.flush(body));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const start = async (add = false): Promise<void> => {
    const router = TestBed.inject(Router);
    await router.navigate(['/savings/reservations'], {
      queryParams: { budgetId: 'b1', ...(add ? { add: 1 } : {}) },
    });

    fixture = TestBed.createComponent(Reservations);
    http = TestBed.inject(HttpTestingController);
  };

  beforeEach(async () => {
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [Reservations],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'dashboard', children: [] },
          { path: 'savings', children: [] },
          { path: 'savings/reservations', children: [] },
        ]),
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
      reservations: {
        title: 'Wszystkie rezerwacje',
        summary: '{{reserved}} zł zarezerwowane · {{settled}} zł rozliczone · wolne środki {{free}} zł',
        summaryOver: '{{reserved}} zł zarezerwowane · {{settled}} zł rozliczone · brak wolnych środków',
        balanceNote: 'Stan konta oszczędnościowego: {{balance}} zł. To nie jest saldo konta.',
        coveredBy: 'Resztę uzbierasz do: {{month}}.',
        add: 'Dodaj rezerwację',
        settle: 'Rozlicz',
        unsettle: 'Cofnij rozliczenie',
        edit: 'Edytuj',
        delete: 'Usuń',
        empty: 'Nie ma jeszcze żadnych rezerwacji.',
        columns: { name: 'Nazwa', amount: 'Kwota', due: 'Termin', collected: 'Uzbierane', status: 'Status' },
        status: { collecting: 'Zbiera', overdue: 'Po terminie', settled: 'Rozliczona' },
        overReserved: {
          title: 'Rezerwacje przekraczają stan konta o {{amount}} zł.',
          body: 'To nie jest błąd.',
        },
        editor: {
          addTitle: 'Dodaj rezerwację',
          editTitle: 'Edytuj rezerwację',
          saveEdit: 'Zapisz',
          name: 'Nazwa',
          namePlaceholder: 'Ubezpieczenie OC',
          amount: 'Kwota',
          due: 'Termin',
          queueNote: 'Dodanie rezerwacji z bliższym terminem PRZESUNIE KOLEJKĘ i cofnie postęp pozostałych.',
        },
        settleModal: {
          title: 'Czy to było: {{name}}?',
          pick: 'Wskaż wypłatę.',
          noCandidates: 'Nie widzę żadnej nierozliczonej wypłaty.',
          confirm: 'Tak, rozlicz',
          cancel: 'To co innego',
          freeFundsNote: 'Rozliczenie NIE zwiększy wolnych środków — koperta jest roczna.',
          manualNote: 'Nigdy nie rozliczam automatycznie.',
        },
        deleteConfirm: { header: 'Usunąć „{{name}}"?', description: 'Wolne środki wrócą.' },
        unsettleConfirm: { header: 'Cofnąć „{{name}}"?', description: 'Wróci do kolejki.' },
        errors: { loadFailed: 'Nie udało się wczytać rezerwacji' },
      },
    });
    translate.use('pl');
  });

  afterEach(() => {
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
  });

  // ── Przełącznik budżetu ──────────────────────────────────────────────────────────────

  it('pokazuje przełącznik budżetu z budżetem, na którym policzono listę', async () => {
    await start();
    await settleList();

    const switcher = fixture.debugElement.query(By.directive(BudgetSwitcher));
    expect(switcher).not.toBeNull();
    expect(switcher.componentInstance.selectedId()).toBe('b1');
    expect(switcher.componentInstance.budgets().map((b: { name: string }) => b.name))
      .toEqual(['Budżet domowy', 'Wariant']);
  });

  it('wybór innego budżetu zmienia ADRES i przelicza listę na nowym budżecie', async () => {
    // ⚠️ Wybór musi iść przez adres: `budgetId` w adresie wygrywa z `ActiveBudget`, więc
    // ustawienie samego `ActiveBudget` zostawiłoby listę na poprzednim budżecie.
    await start(true);
    await settleList();

    const switcher = fixture.debugElement.query(By.directive(BudgetSwitcher));
    switcher.componentInstance.budgetChange.emit('b2');
    // Nie `whenStable()`: nowe żądanie jeszcze wisi, więc czekanie na stabilność nigdy by się nie skończyło.
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    expect(router.parseUrl(router.url).queryParamMap.getAll('budgetId')).toEqual(['b2']);
    // Reszta adresu zostaje — `?add=1` nie może zniknąć przy okazji.
    expect(router.parseUrl(router.url).queryParamMap.get('add')).toBe('1');

    const next = http.match((r) => r.url === '/api/savings/reservations');
    expect(next.at(-1)?.request.params.getAll('budgetId')).toEqual(['b2']);
    next.forEach((r) => r.flush(response({ selectedBudgetIds: ['b2'] })));
    await fixture.whenStable();
    expect(TestBed.inject(ActiveBudget).ids()).toEqual(['b2']);
  });

  // ── Wolne środki ─────────────────────────────────────────────────────────────────────

  it('pokazuje wolne środki policzone przez serwer, także gdy część rezerwacji jest rozliczona', async () => {
    // ⚠️ 9 400 − 5 000 = 4 400 MIMO rozliczonych 1 800 zł (makieta 147:96, issue #11).
    // Koperta jest roczna, więc rozliczona rezerwacja dalej obciąża wolne środki.
    // Ekran NIE liczy tego po swojemu — gdyby liczył, miałby drugą kopię reguły.
    await start();
    await settleList();

    expect(text()).toContain('wolne środki 4400,00');
  });

  it('ujemne wolne środki pokazuje jako osobny stan, a nie jako minus przy etykiecie', async () => {
    await start();
    await settleList(response({ accountBalance: 1000, reservedTotal: 1600, freeFunds: -600 }));

    // Minus przy słowach „wolne środki" czyta się jak błąd aplikacji, a nie jak stan finansów.
    expect(text()).toContain('przekraczają stan konta o 600,00');
    // Nagłówek nie może wydrukować „wolne środki -600,00 zł" — to zdanie wewnętrznie
    // sprzeczne. Ten test złapał dokładnie taką regresję przy pierwszym przebiegu.
    expect(text()).toContain('brak wolnych środków');
    expect(text()).not.toContain('-600,00');
  });

  it('mówi wprost, że stan konta to fallback, a nie saldo', async () => {
    // Przed #10 stan liczy się z kategorii, więc podanie go bez zastrzeżenia byłoby
    // stwierdzeniem faktu, którego nikt nie sprawdził.
    await start();
    await settleList();

    expect(text()).toContain('To nie jest saldo konta.');
  });

  // ── Wiersze ──────────────────────────────────────────────────────────────────────────

  it('zostawia rozliczoną rezerwację w tabeli, bo ta dalej liczy się do wolnych środków', async () => {
    await start();
    await settleList(response({
      reservations: [row({ status: 'Settled', settledOn: '2026-08-14' })],
    }));

    // Ukrycie jej sprawiłoby, że suma w nagłówku nie zgadza się z widocznymi wierszami.
    expect(text()).toContain('Ubezpieczenie OC');
    expect(text()).toContain('Rozliczona');
    // Rozliczona nie ma już czego rozliczać — zamiast tego oferuje cofnięcie.
    expect(text()).toContain('Cofnij rozliczenie');
    expect(text()).not.toContain('Rozlicz ');
  });

  it('rezerwację po terminie oznacza osobnym statusem', async () => {
    await start();
    await settleList(response({ reservations: [row({ status: 'Overdue', collected: 250 })] }));

    // Bez tego stanu nierozliczona rezerwacja po terminie zaniżałaby wolne środki
    // w nieskończoność i nic by o tym nie powiedziało.
    expect(text()).toContain('Po terminie');
  });

  // ── Modal dodawania ──────────────────────────────────────────────────────────────────

  it('ostrzega o przesunięciu kolejki ZANIM pozwoli zapisać', async () => {
    await start();
    await settleList();

    fixture.componentInstance['openEditor'](null);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    // To zdanie jest jedynym miejscem, w którym da się wytłumaczyć, dlaczego postęp innych
    // rezerwacji zaraz się cofnie. Po zapisie byłoby już tylko wyjaśnianiem zaskoczenia.
    expect(documentText()).toContain('PRZESUNIE KOLEJKĘ');
  });

  it('otwiera modal dodawania, gdy przyszło się z kafla przez ?add=1', async () => {
    // Kafel na ekranie oszczędności ma przycisk „Dodaj rezerwację" (makieta 147:96),
    // ale formularz żyje tutaj — dwie kopie rozjechałyby się przy pierwszej poprawce.
    await start(true);
    await settleList();

    expect(documentText()).toContain('Dodaj rezerwację');
    expect(documentText()).toContain('PRZESUNIE KOLEJKĘ');
  });

  it('wysyła termin jako PIERWSZY dzień wybranego miesiąca, licząc go lokalnie', async () => {
    // ⚠️ `toISOString()` przeliczyłby 1 czerwca 00:00 czasu lokalnego na 31 maja 22:00 UTC,
    // czyli termin cofnąłby się o miesiąc. Serwer normalizuje datę do pierwszego dnia — ale
    // normalizowałby już ZŁY miesiąc, więc błąd nie miałby gdzie wyjść na jaw.
    await start();
    await settleList();

    const api = fixture.componentInstance as unknown as {
      openEditor(row: SavingsReservation | null): void;
      draftName: { set(v: string): void };
      draftAmount: { set(v: number): void };
      draftDue: { set(v: Date): void };
      save(): Promise<void>;
    };

    api.openEditor(null);
    api.draftName.set('Kurs nurkowania');
    api.draftAmount.set(400);
    api.draftDue.set(new Date(2026, 5, 1));
    void api.save();
    await fixture.whenStable();

    const request = http.expectOne('/api/savings/reservations');
    expect((request.request.body as { dueMonth: string }).dueMonth).toBe('2026-06-01');
    request.flush({ ...row(), dueMonth: '2026-06-01' });
    await settleList();
  });

  // ── Modal rozliczenia ────────────────────────────────────────────────────────────────

  it('pobiera kandydatów dopiero przy otwarciu modala i mówi, że rozliczenie nie zwalnia środków', async () => {
    await start();
    await settleList();

    // Lista już się wczytała, a żadnego zapytania o kandydatów nie było — inaczej każdy
    // wiersz ciągnąłby własne, żeby najczęściej nie pokazać nic.
    http.expectNone('/api/savings/reservations/r1/settle-candidates');

    void fixture.componentInstance['openSettle'](row());
    fixture.detectChanges();

    const candidates: SettleCandidate[] = [
      { id: 't1', date: '2026-08-14', amount: 1800, description: 'PRZELEW WLASNY' },
    ];
    http.expectOne('/api/savings/reservations/r1/settle-candidates').flush(candidates);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(documentText()).toContain('PRZELEW WLASNY');
    // ⚠️ Bez tego zdania użytkownik rozliczy ubezpieczenie, zobaczy, że wolne środki się
    // nie ruszyły, i uzna to za błąd. Reguła jest przeciwintuicyjna, więc musi być napisana.
    expect(documentText()).toContain('NIE zwiększy wolnych środków');
    expect(documentText()).toContain('Nigdy nie rozliczam automatycznie');
  });

  it('bez kandydatów tłumaczy, dlaczego nie da się rozliczyć ręcznym odhaczeniem', async () => {
    await start();
    await settleList();

    void fixture.componentInstance['openSettle'](row());
    fixture.detectChanges();
    http.expectOne('/api/savings/reservations/r1/settle-candidates').flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(documentText()).toContain('Nie widzę żadnej nierozliczonej wypłaty');
  });

  // ── Usuwanie ─────────────────────────────────────────────────────────────────────────

  it('przed usunięciem pyta i nazywa rezerwację po imieniu', async () => {
    await start();
    await settleList();

    void fixture.componentInstance['remove'](row());
    await fixture.whenStable();

    expect(confirmDialog.lastOptions?.header).toContain('Ubezpieczenie OC');
    confirmDialog.respond(false);
    await fixture.whenStable();

    // Odmowa nie może niczego wysłać.
    http.expectNone((r) => r.method === 'DELETE');
  });
});
