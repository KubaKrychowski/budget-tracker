import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { APP_ICONS } from '../../core/icons';
import { SavingsMonth, SavingsResponse } from '../../core/api/models/savings';
import { ReservationsResponse } from '../../core/api/models/reservations';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../core/confirm-dialog/confirm-dialog-options';
import { ActiveBudget } from '../../core/active-budget';
import { By } from '@angular/platform-browser';
import { BudgetSwitcher } from '../../core/budget-switcher/budget-switcher';
import { Savings } from './savings';

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
 * Ekran „Cele oszczędzania".
 *
 * Testujemy to, co ekran MÓWI, a nie jak wygląda — bo cała wartość tej funkcji leży w jednym
 * zdaniu i w warunkach, w jakich wolno je postawić. Każdy stan z tabeli w issue #7 ma tu wpis.
 */
describe('Savings', () => {
  let fixture: ComponentFixture<Savings>;
  let http: HttpTestingController;
  let confirmDialog: FakeConfirmDialogService;

  const month = (over: Partial<SavingsMonth> = {}): SavingsMonth => ({
    month: '2026-10-01',
    deposited: 1500,
    withdrawn: 0,
    goal: 1500,
    oneOffTotal: 900,
    oneOffCount: 1,
    verdict: 'Proof',
    ...over,
  });

  const response = (over: Partial<SavingsResponse> = {}): SavingsResponse => ({
    goal: { id: 'g1', amount: 1500, startedOn: '2026-01-01' },
    months: [month()],
    currentMonth: month({ month: '2026-11-01', deposited: 1200, verdict: 'GoalMissed', oneOffCount: 0, oneOffTotal: 0 }),
    proofCount: 1,
    depositedThisYear: 14200,
    raiseSuggestion: null,
    hasAnyEpisodicExpense: true,
    hasAnySavings: true,
    selectedBudgetIds: ['b1'],
    budgets: [
      { id: 'b1', name: 'Budżet domowy', month: '2026-11-01', disabled: false },
      { id: 'b2', name: 'Wariant', month: '2026-10-01', disabled: false },
    ],
    ...over,
  });

  const text = (): string =>
    (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  /**
   * Odpowiedź kafla rezerwacji — pusta, bo te testy dotyczą CELU, a nie rezerwacji.
   * Tamte mają własny plik; tutaj chodzi tylko o to, żeby żądanie zostało obsłużone.
   */
  const reservations: ReservationsResponse = {
    reservations: [],
    accountBalance: 0,
    reservedTotal: 0,
    settledTotal: 0,
    collectedTotal: 0,
    availableToContribute: 0,
    freeFunds: 0,
    coveredBy: null,
    selectedBudgetIds: ['b1'],
    budgets: [],
  };

  const settle = async (
    body: SavingsResponse = response(),
    reservationsBody: ReservationsResponse = reservations,
  ): Promise<void> => {
    fixture.detectChanges();
    http.match('/api/savings').forEach((r) => r.flush(body));
    // ⚠️ Kafel rezerwacji ciągnie DRUGIE żądanie i ono też musi tu paść. Nieobsłużone
    // `httpResource` zostaje wiszącym zadaniem, więc `whenStable()` nigdy się nie kończy —
    // każdy test tego pliku wygląda wtedy na timeout, bez śladu, że chodzi o rezerwacje.
    http.match((r) => r.url === '/api/savings/reservations')
      .forEach((r) => r.flush(reservationsBody));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [Savings],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'dashboard', children: [] },
          { path: 'transactions', children: [] },
          { path: 'episodic-orders', children: [] },
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
      savings: {
        states: {
          noGoal: { title: 'Nie masz jeszcze celu oszczędnościowego.', body: 'Ustaw kwotę.' },
          noEpisodicExpense: {
            title: 'Nie masz jeszcze żadnego zrealizowanego zlecenia epizodycznego.',
            body: 'Bez nich dowód nie powstanie.',
            cta: 'Przejdź do zleceń epizodycznych',
          },
          proof: {
            title: 'Odłożyłeś {{amount}} zł także w {{month}} — miesiącu z {{oneOff}} zł jednorazowych.',
            body: 'Ten cel jest bezpieczny. Liczba miesięcy z dowodem: {{count}}.',
          },
          missed: {
            title: 'W {{month}} zabrakło {{missing}} zł do celu.',
            body: 'Doszło {{oneOff}} zł jednorazowych. To informacja, nie ocena.',
          },
        },
        noSavingsSeen: {
          title: 'Nie widzę żadnych wpłat na oszczędności.',
          body: 'Sprawdź kategorię.',
        },
        verdict: { proof: 'Dowód', goalMet: 'Cel osiągnięty', missed: 'Nieosiągnięty', noGoal: 'Bez celu', inProgress: 'W toku' },
        setGoal: 'Ustaw cel',
        raiseGoal: 'Podnieś cel do {{amount}} zł',
        raiseSuggestion: 'Masz {{count}} miesiące z dowodem. Zapas ok. {{headroom}} zł.',
        endConfirm: { header: 'Zrezygnować z celu?', description: 'Zostanie zakończony datą.' },
      },
      reservations: {
        tileTitle: 'Rezerwacje na ten rok',
        tileSummary: '{{reserved}} zł zarezerwowane · {{settled}} zł już rozliczone',
        tileEmpty: 'Nie masz jeszcze żadnych rezerwacji.',
        collected: 'Uzbierane',
        freeFunds: 'Wolne środki: {{amount}} zł',
        freeFundsBody: 'Stan konta oszczędnościowego {{balance}} zł − {{reserved}} zł rezerwacji.',
        balanceCaveat: 'Stan konta liczę z kategorii — to nie jest saldo konta.',
        coveredBy: 'Resztę uzbierasz do: {{month}}.',
        add: 'Dodaj rezerwację',
        showAll: 'Wszystkie rezerwacje',
        overReserved: {
          title: 'Rezerwacje przekraczają stan konta o {{amount}} zł.',
          body: 'To nie jest błąd.',
        },
        status: { collecting: 'Zbiera', overdue: 'Po terminie', settled: 'Rozliczona' },
      },
    });
    translate.use('pl');

    fixture = TestBed.createComponent(Savings);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
  });

  // ── Budżet w widoku (issue #16) ──────────────────────────────────────────────────────

  it('bez budgetId w adresie liczy na budżecie, który użytkownik ma w widoku', async () => {
    // ⚠️ Regresja #16: wejście z wyszukiwarki w nagłówku nie niesie budgetId. Wcześniej ekran
    // pytał wtedy o budżet domyślny — a przy kilku budżetach z tym samym miesiącem to ostatnio
    // UTWORZONY, więc świeży, pusty budżet zastępował ten wybrany na dashboardzie.
    TestBed.inject(ActiveBudget).set(['budzet-z-dashboardu']);

    fixture.detectChanges();
    const savings = http.match((r) => r.url === '/api/savings');
    const reservationsRequests = http.match((r) => r.url === '/api/savings/reservations');

    expect(savings.length).toBe(1);
    expect(savings[0].request.params.getAll('budgetId')).toEqual(['budzet-z-dashboardu']);
    expect(reservationsRequests[0]?.request.params.getAll('budgetId')).toEqual(['budzet-z-dashboardu']);

    savings.forEach((r) => r.flush(response()));
    reservationsRequests.forEach((r) => r.flush(reservations));
    await fixture.whenStable();
  });

  it('bez budżetu w widoku i w adresie zostawia wybór backendowi', async () => {
    fixture.detectChanges();
    const savings = http.match((r) => r.url === '/api/savings');

    expect(savings[0].request.params.has('budgetId')).toBe(false);

    savings.forEach((r) => r.flush(response()));
    http.match((r) => r.url === '/api/savings/reservations').forEach((r) => r.flush(reservations));
    await fixture.whenStable();
  });

  // ── Przełącznik budżetu ──────────────────────────────────────────────────────────────

  it('bez budgetId w adresie przełącznik pokazuje budżet, który wybrał SERWER', async () => {
    // Serwer bierze wtedy budżet domyślny — przełącznik ma pokazać właśnie jego, a nie pustkę,
    // bo „Wybierz budżet" przy wyliczonych już liczbach sugerowałoby, że nic nie jest wybrane.
    await settle(response({ selectedBudgetIds: ['b2'] }));

    const switcher = fixture.debugElement.query(By.directive(BudgetSwitcher));
    expect(switcher.componentInstance.selectedId()).toBe('b2');
  });

  it('wybór innego budżetu przelicza cel i kafel rezerwacji na nowym budżecie', async () => {
    await settle();

    fixture.debugElement.query(By.directive(BudgetSwitcher)).componentInstance.budgetChange.emit('b2');
    // Nie `whenStable()`: nowe żądania jeszcze wiszą, więc czekanie na stabilność nigdy by się nie skończyło.
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();

    const savings = http.match((r) => r.url === '/api/savings');
    const reservationsRequests = http.match((r) => r.url === '/api/savings/reservations');
    expect(savings.at(-1)?.request.params.getAll('budgetId')).toEqual(['b2']);
    expect(reservationsRequests.at(-1)?.request.params.getAll('budgetId')).toEqual(['b2']);

    // Przełącznik NIE znika w trakcie przeładowania — zostaje ostatnia znana lista.
    expect(fixture.debugElement.query(By.directive(BudgetSwitcher))).not.toBeNull();

    savings.forEach((r) => r.flush(response({ selectedBudgetIds: ['b2'] })));
    reservationsRequests.forEach((r) => r.flush(reservations));
    await fixture.whenStable();
    expect(TestBed.inject(ActiveBudget).ids()).toEqual(['b2']);
  });

  // ── Stany ekranu ─────────────────────────────────────────────────────────────────────

  it('bez celu prosi o ustawienie kwoty, zamiast oceniać czegokolwiek', async () => {
    // ⚠️ Ekran, który bez celu pokazuje „cel nieosiągnięty", oskarża użytkownika
    // o niedotrzymanie czegoś, czego nigdy nie zadeklarował.
    await settle(response({ goal: null, months: [], proofCount: 0 }));

    expect(text()).toContain('Nie masz jeszcze celu');
    expect(text()).not.toContain('zabrakło');
  });

  it('bez zrealizowanych zleceń epizodycznych mówi, czego potrzebuje — nie „0 dowodów"', async () => {
    // Stan WEJŚCIA w funkcję: zlecenia zakłada się ręcznie, więc na zimnym starcie jest ich zero.
    await settle(response({ hasAnyEpisodicExpense: false, months: [month({ verdict: 'GoalMet', oneOffCount: 0 })] }));

    expect(text()).toContain('Nie masz jeszcze żadnego zrealizowanego zlecenia epizodycznego');
    expect(text()).toContain('Przejdź do zleceń epizodycznych');
  });

  it('dowód mówi o warunkach, w jakich cel wyszedł — nie o samej kwocie', async () => {
    // To jest całe zdanie, dla którego ten ekran istnieje.
    await settle();

    expect(text()).toContain('także w');
    expect(text()).toContain('jednorazowych');
    expect(text()).toContain('Ten cel jest bezpieczny');
  });

  it('niedobór jest podany bez oceniania', async () => {
    await settle(response({
      months: [month({ month: '2026-09-01', deposited: 900, verdict: 'GoalMissed' })],
      proofCount: 0,
    }));

    expect(text()).toContain('zabrakło');
    expect(text()).toContain('To informacja, nie ocena');
  });

  it('NIE ogłasza niedoboru w miesiącu, który jeszcze trwa', async () => {
    // ⚠️ „Zabrakło 600 zł" postawione 15. dnia miesiąca jest po prostu nieprawdą — miesiąc
    // trwa i nic jeszcze nie zabrakło. Postęp bieżącego miesiąca pokazuje kafel.
    const current = month({ month: '2026-11-01', deposited: 900, verdict: 'GoalMissed' });
    await settle(response({
      months: [current],
      currentMonth: current,
      proofCount: 0,
      hasAnyEpisodicExpense: true,
    }));

    expect(text()).not.toContain('zabrakło');
  });

  it('ostrzega, gdy nie widzi wpłat — zamiast pokazywać zero jako fakt', async () => {
    // Przed #10 odkładanie rozpoznaje kategoria, nie konto: przelew opisany inaczej przepadnie.
    await settle(response({ hasAnySavings: false }));

    expect(text()).toContain('Nie widzę żadnych wpłat');
  });

  // ── Propozycja podniesienia ──────────────────────────────────────────────────────────

  it('bez propozycji nie pokazuje przycisku podnoszenia celu', async () => {
    await settle(response({ raiseSuggestion: null }));

    expect(text()).not.toContain('Podnieś cel');
  });

  it('propozycja podniesienia jest ODDZIELONA od zdania o odporności', async () => {
    // Dowód opisuje przeszłość i jest faktem; propozycja jest ekstrapolacją. Zlanie ich
    // w jeden komunikat zamieniłoby fakt w zachętę.
    await settle(response({
      proofCount: 2,
      raiseSuggestion: { headroom: 900, suggestedAmount: 2400 },
    }));

    expect(text()).toContain('Ten cel jest bezpieczny');
    expect(text()).toContain('Zapas ok.');
    expect(text()).toContain('Podnieś cel do');
  });

  // ── Tabela ───────────────────────────────────────────────────────────────────────────

  it('wypłata jest widoczna OBOK wpłaty, nie odjęta od niej', async () => {
    // Tak wygląda miesiąc z ubezpieczeniem opłaconym z oszczędności — dalej dowód.
    await settle(response({
      months: [month({ deposited: 1500, withdrawn: 1800, verdict: 'Proof' })],
    }));

    // `Intl` dla pl-PL nie grupuje liczb czterocyfrowych (1500 zostaje bez spacji, 14 200 już z nią) —
    // to zachowanie standardowe i takie samo jak na dashboardzie oraz liście transakcji.
    expect(text()).toContain('1500,00');
    expect(text()).toContain('1800,00');
    expect(text()).toContain('Dowód');
  });

  it('miesiąc, który TRWA, nie dostaje werdyktu „nieosiągnięty"', async () => {
    // Piątego dnia miesiąca nic jeszcze nie zabrakło. Dowód jest odwrotnie — raz spełniony,
    // jest prawdą od razu i nie ma powodu z nim czekać.
    const current = month({
      month: '2026-11-01', deposited: 200, verdict: 'GoalMissed', oneOffCount: 0, oneOffTotal: 0,
    });
    await settle(response({ months: [current], currentMonth: current, proofCount: 0 }));

    expect(text()).toContain('W toku');
    expect(text()).not.toContain('Nieosiągnięty');
  });

  it('miesiąc sprzed ustawienia celu nie dostaje werdyktu', async () => {
    await settle(response({
      months: [month({ goal: null, verdict: 'NoGoal', oneOffCount: 0, oneOffTotal: 0 })],
      proofCount: 0,
    }));

    expect(text()).toContain('Bez celu');
  });

  // ── Akcje ────────────────────────────────────────────────────────────────────────────

  it('„Pokaż zlecenia epizodyczne” prowadzi do zrealizowanych tego budżetu, niczego nie zdejmując', async () => {
    // Zlecenie ma nazwę i opis — usuwa się je pojedynczo na jego ekranie, nie hurtem z miesiąca.
    await settle();

    const component = fixture.componentInstance as unknown as { showEpisodic(): void };
    component.showEpisodic();
    // Bez whenStable — nawigacja zmienia adres, a nowe żądanie ekranu zostałoby wiszącym zadaniem.
    await new Promise((resolve) => setTimeout(resolve));

    http.expectNone((r) => r.method !== 'GET');
    const router = TestBed.inject(Router);
    const url = router.parseUrl(router.url);
    expect(url.root.children['primary']?.segments.map((s) => s.path)).toEqual(['episodic-orders']);
    expect(url.queryParamMap.get('tab')).toBe('realized');
  });

  it('rezygnacja z celu pyta, zanim cokolwiek zrobi', async () => {
    await settle();

    const component = fixture.componentInstance as unknown as { endGoal(): Promise<void> };
    void component.endGoal();
    await fixture.whenStable();

    expect(confirmDialog.lastOptions?.header).toContain('Zrezygnować');

    confirmDialog.respond(false);
    await fixture.whenStable();

    http.expectNone('/api/savings/goal');
  });

  // ── Kafel rezerwacji ─────────────────────────────────────────────────────────────────

  it('kafel rezerwacji podaje wolne środki i ich rozpisanie, także gdy część jest rozliczona', async () => {
    // ⚠️ Rozliczona rezerwacja dalej obciąża wolne środki — koperta jest roczna (issue #11,
    // makieta 147:96: 9 400 − 5 000 = 4 400 przy rozliczonych 1 800). Kafel ma to POKAZAĆ
    // w rozpisaniu, bo sama liczba „4 400" nie tłumaczy, skąd się wzięła.
    await settle(response(), {
      ...reservations,
      reservations: [
        {
          id: 'r1', name: 'Ubezpieczenie OC', amount: 1800, dueMonth: '2026-05-01',
          collected: 1800, status: 'Settled', settledOn: '2026-08-14', settledTransactionId: 't1', contributions: [],
        },
      ],
      accountBalance: 9400,
      reservedTotal: 5000,
      settledTotal: 1800,
      collectedTotal: 1800,
    availableToContribute: 0,
      freeFunds: 4400,
    });

    expect(text()).toContain('Rezerwacje na ten rok');
    expect(text()).toContain('5000 zł zarezerwowane · 1800 zł już rozliczone');
    expect(text()).toContain('Wolne środki: 4400 zł');
    expect(text()).toContain('Stan konta oszczędnościowego 9400 zł − 5000 zł rezerwacji.');
    // Przed #10 stan konta to fallback po kategorii — i kafel musi to powiedzieć.
    expect(text()).toContain('to nie jest saldo konta');
  });

  it('rysuje rezerwacje jako WSPÓŁŚRODKOWE pierścienie, po jednym na rezerwację', async () => {
    // ⚠️ Kształt jest tu treścią, nie ozdobą: `radialBar` z jedną serią na rezerwację pokazuje
    // je jako podział JEDNEJ puli, a rząd osobnych kółek postępu — jako niepowiązane paski.
    // Pierwsza wersja miała właśnie osobne kółka i rozminęła się z makietą 147:96.
    await settle(response(), {
      ...reservations,
      reservations: [
        { id: 'r1', name: 'Ubezpieczenie', amount: 1800, dueMonth: '2027-05-01', collected: 900, status: 'Collecting', settledOn: null, settledTransactionId: null, contributions: [] },
        { id: 'r2', name: 'Aparat', amount: 1600, dueMonth: '2027-09-01', collected: 0, status: 'Collecting', settledOn: null, settledTransactionId: null, contributions: [] },
      ],
      reservedTotal: 3400,
      collectedTotal: 900,
    availableToContribute: 0,
    });

    const chart = (fixture.componentInstance as unknown as {
      reservationChart(): { chart: { type: string }; series: number[]; labels: string[] };
    }).reservationChart();

    expect(chart.chart.type).toBe('radialBar');
    expect(chart.series).toEqual([50, 0]);
    expect(chart.labels).toEqual(['Ubezpieczenie', 'Aparat']);
  });

  it('kafel nie drukuje ujemnych wolnych środków, tylko nazywa nadmiar rezerwacji', async () => {
    await settle(response(), {
      ...reservations,
      accountBalance: 1000,
      reservedTotal: 1600,
      freeFunds: -600,
    });

    expect(text()).toContain('przekraczają stan konta o 600,00');
    expect(text()).not.toContain('Wolne środki: -600');
  });
});
