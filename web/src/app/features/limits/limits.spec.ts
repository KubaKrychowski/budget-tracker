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
import { ActiveBudget } from '../../core/active-budget';
import { LimitRow, LimitsResponse } from '../../core/api/models/limits';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../core/confirm-dialog/confirm-dialog-options';
import { Limits } from './limits';

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

/** Dostęp do chronionych członków komponentu — testy sterują modalem tak jak robiłby to szablon. */
interface LimitsApi {
  openEditor(row: LimitRow | null, categoryId?: string | null): void;
  draftCategoryId: { set(v: string | null): void };
  draftAmount: { set(v: number | null): void };
  draftThreshold: { set(v: number | null): void };
  draftValidFrom: { set(v: Date | null): void };
  save(): Promise<void>;
  remove(row: LimitRow): Promise<void>;
  shiftMonth(delta: number): void;
  goToCurrentMonth(): void;
  disabledMonth(date: Date): boolean;
}

/**
 * Ekran „Limity wydatków". Sprawdzamy to, co ekran MÓWI i co wysyła — kolor paska i stan liczy serwer.
 */
describe('Limits', () => {
  let fixture: ComponentFixture<Limits>;
  let http: HttpTestingController;
  let confirmDialog: FakeConfirmDialogService;

  const row = (over: Partial<LimitRow> = {}): LimitRow => ({
    id: 'l1',
    categoryId: 'c-food',
    categoryName: 'Jedzenie',
    limit: 1200,
    spent: 980,
    remaining: 220,
    percent: 82,
    warningThreshold: 80,
    state: 'Warning',
    validFrom: '2026-09-01',
    nextLimit: null,
    nextValidFrom: null,
    ...over,
  });

  const response = (over: Partial<LimitsResponse> = {}): LimitsResponse => ({
    month: '2026-09-01',
    currentMonth: '2026-09-01',
    readOnly: false,
    limits: [row()],
    unlimited: [{ categoryId: 'c-gift', categoryName: 'Prezenty', spent: 260 }],
    categories: [
      { id: 'c-food', name: 'Jedzenie', hasLimit: true },
      { id: 'c-gift', name: 'Prezenty', hasLimit: false },
    ],
    limitTotal: 1200,
    spentInLimited: 980,
    spentOutside: 260,
    overLimitCount: 0,
    uncategorizedCount: 0,
    uncategorizedAmount: 0,
    selectedBudgetIds: ['b1'],
    budgets: [{ id: 'b1', name: 'Domowy', month: '2026-09-01', disabled: false }],
    ...over,
  });

  const api = (): LimitsApi => fixture.componentInstance as unknown as LimitsApi;

  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  const settle = async (body: LimitsResponse = response()): Promise<void> => {
    fixture.detectChanges();
    http.match((r) => r.url === '/api/limits').forEach((r) => r.flush(body));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const queryParam = (name: string): string | null => {
    const router = TestBed.inject(Router);
    return router.parseUrl(router.url).queryParamMap.get(name);
  };

  beforeEach(async () => {
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [Limits],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'dashboard', children: [] }, { path: 'limits', children: [] }]),
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
      dashboard: { configuration: 'Konfiguracja' },
      limits: {
        addFirst: 'Dodaj pierwszy limit',
        title: 'Limity wydatków',
        add: 'Dodaj limit',
        change: 'Zmień',
        remove: 'Usuń',
        currentMonth: 'Bieżący miesiąc',
        banner: {
          noLimits: 'Ten budżet nie ma jeszcze limitów.',
          noLimitsClosed: '{{month}} — w tym miesiącu budżet nie miał żadnych limitów.',
          overOne: 'Ponad limitem: {{name}} — {{spent}} zł z {{limit}} zł.',
          overMany: 'Kategorie ponad limitem: {{count}}.',
          closedFine: '{{month}} — miesiąc zamknięty.',
          uncategorized: 'Wydatki bez kategorii ({{count}}, razem {{amount}} zł) nie liczą się do żadnego limitu.',
        },
        table: {
          nextLimit: 'od {{month}}: {{amount}} zł',
          overBy: '{{percent}}% · {{amount}} zł ponad',
          empty: 'Brak limitów w tym miesiącu',
          emptyBudget: 'Brak limitów w tym budżecie',
        },
        unlimited: {
          set: 'Ustaw limit',
          empty: 'Każda kategoria z wydatkami w tym miesiącu ma już limit.',
          noSpending: 'W tym miesiącu nie ma jeszcze wydatków z kategorią.',
          note: 'Te {{amount}} zł nie wchodzi do miesięcznego limitu budżetu.',
        },
        removeConfirm: { header: 'Usunąć limit „{{name}}”?', description: 'spadnie do {{total}} zł' },
      },
    });
    translate.use('pl');

    await TestBed.inject(Router).navigate(['/limits']);
    fixture = TestBed.createComponent(Limits);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
  });

  // ── Budżet i miesiąc ─────────────────────────────────────────────────────────────────

  it('bez budgetId w adresie pyta o JEDEN budżet z widoku, a bez niego zostawia wybór serwerowi', async () => {
    TestBed.inject(ActiveBudget).set(['z-dashboardu', 'drugi']);
    fixture.detectChanges();

    const [request] = http.match((r) => r.url === '/api/limits');
    expect(request.request.params.getAll('budgetId')).toEqual(['z-dashboardu']);
    expect(request.request.params.has('month')).toBe(false);
    request.flush(response());
    await fixture.whenStable();
  });

  it('strzałka zmienia miesiąc w ADRESIE, a „Bieżący miesiąc" go zdejmuje', async () => {
    await settle();

    // Nie `whenStable()`: zmiana adresu odpala nowe żądanie, a wiszące żądanie nie pozwala się ustabilizować.
    const tick = () => new Promise((resolve) => setTimeout(resolve));

    api().shiftMonth(-1);
    await tick();
    expect(queryParam('month')).toBe('2026-08-01');

    api().goToCurrentMonth();
    await tick();
    expect(queryParam('month')).toBeNull();
  });

  // ── Baner ────────────────────────────────────────────────────────────────────────────

  it('bez limitów w bieżącym miesiącu: jedno wezwanie „Dodaj pierwszy limit", bez tabeli i paska miesięcy', async () => {
    // Makieta 203:13079 — pusty stan nie ma strzałek miesięcy, tylko zdanie i przycisk.
    await settle(response({ limits: [], limitTotal: 0, spentInLimited: 0 }));

    expect(text()).toContain('Ten budżet nie ma jeszcze limitów.');
    expect(text()).toContain('Brak limitów w tym budżecie');
    expect(text()).toContain('Dodaj pierwszy limit');
    expect(text()).not.toContain('Bieżący miesiąc');
    expect(fixture.nativeElement.querySelector('.lim__track')).toBeNull();
  });

  it('przełącznik budżetu siedzi w karcie „Konfiguracja"', async () => {
    await settle();

    const config = fixture.nativeElement.querySelector('.lim__config') as HTMLElement;
    expect(config.textContent).toContain('Konfiguracja');
    expect(config.querySelector('app-budget-switcher')).not.toBeNull();
  });

  it('przekroczenie nazywa kategorię i kwoty, a wydatki bez kategorii dostają osobne zdanie', async () => {
    await settle(response({
      limits: [row({ categoryName: 'Rozrywka', limit: 350, spent: 420, remaining: -70, percent: 120, state: 'Over' })],
      overLimitCount: 1,
      uncategorizedCount: 6,
      uncategorizedAmount: 310,
    }));

    expect(text()).toContain('Ponad limitem: Rozrywka — 420,00 zł z 350,00 zł.');
    expect(text()).toContain('Wydatki bez kategorii (6, razem 310,00 zł) nie liczą się do żadnego limitu.');
    expect(text()).toContain('120% · 70,00 zł ponad');
  });

  it('bez przekroczeń i bez wydatków bez kategorii nie pokazuje żadnego baneru', async () => {
    // Decyzja użytkownika: zielony „wszystko w limicie" jest zbędny — tabela mówi to sama.
    await settle();

    expect(fixture.nativeElement.querySelector('.lim__banner')).toBeNull();
  });

  it('bez przekroczeń, ale z wydatkami bez kategorii — zostaje samo zdanie o nich', async () => {
    await settle(response({ uncategorizedCount: 2, uncategorizedAmount: 316.17 }));

    const banner = fixture.nativeElement.querySelector('.lim__banner') as HTMLElement;
    expect(banner.textContent).toContain('Wydatki bez kategorii (2, razem 316,17 zł)');
  });

  it('pusta karta „Wydatki bez limitu" mówi, dlaczego jest pusta, i nie pisze o 0,00 zł', async () => {
    await settle(response({ unlimited: [], spentOutside: 0 }));
    expect(text()).toContain('Każda kategoria z wydatkami w tym miesiącu ma już limit.');
    expect(text()).not.toContain('Te 0,00 zł');

    fixture.destroy();
    fixture = TestBed.createComponent(Limits);
    await settle(response({ unlimited: [], spentOutside: 0, spentInLimited: 0, limits: [row({ spent: 0, percent: 0, state: 'Ok' })] }));
    expect(text()).toContain('W tym miesiącu nie ma jeszcze wydatków z kategorią.');
  });

  it('pasek przy przekroczeniu jest pełny, a kreska stoi na progu TEGO limitu', async () => {
    await settle(response({
      limits: [row({ percent: 120, state: 'Over', warningThreshold: 75, spent: 1440, remaining: -240 })],
    }));

    const fill = fixture.nativeElement.querySelector('.lim__fill') as HTMLElement;
    const marker = fixture.nativeElement.querySelector('.lim__marker') as HTMLElement;
    expect(fill.style.width).toBe('100%');
    expect(fill.classList).toContain('lim__fill--over');
    expect(marker.style.left).toBe('75%');
  });

  // ── Miniony miesiąc ──────────────────────────────────────────────────────────────────

  it('zamknięty miesiąc bez limitów nie mówi „budżet nie ma jeszcze limitów"', async () => {
    // Złapane w przeglądarce: budżet z limitami od września, oglądany w sierpniu, zachęcał do ustawienia
    // pierwszego limitu — choć ma je od miesiąca, a sierpnia i tak zmienić się już nie da.
    await settle(response({ month: '2026-08-01', readOnly: true, limits: [], limitTotal: 0 }));

    expect(text()).toContain('Sierpień 2026 — w tym miesiącu budżet nie miał żadnych limitów.');
    expect(text()).not.toContain('Ten budżet nie ma jeszcze limitów.');
  });

  it('miniony miesiąc jest tylko do odczytu i pokazuje kwotę, która obowiązuje później', async () => {
    await settle(response({
      month: '2026-08-01',
      readOnly: true,
      limits: [row({ limit: 1000, state: 'Warning', nextLimit: 1200, nextValidFrom: '2026-09-01' })],
    }));

    expect(text()).toContain('Jedzenie · od IX: 1200,00 zł');
    expect(text()).not.toContain('Dodaj limit');
    expect(text()).not.toContain('Zmień');
    expect(text()).not.toContain('Ustaw limit');
    const current = [...fixture.nativeElement.querySelectorAll('button')]
      .find((b: HTMLButtonElement) => b.textContent?.includes('Bieżący miesiąc')) as HTMLButtonElement;
    expect(current.disabled).toBe(false);
  });

  // ── Zapis ────────────────────────────────────────────────────────────────────────────

  it('zapis wysyła miesiąc jako PIERWSZY dzień, liczony lokalnie, razem z progiem', async () => {
    await settle();

    api().openEditor(null, 'c-gift');
    api().draftAmount.set(300);
    api().draftThreshold.set(90);
    api().draftValidFrom.set(new Date(2026, 9, 17));
    const saving = api().save();

    const request = http.expectOne((r) => r.method === 'POST' && r.url === '/api/limits');
    expect(request.request.body).toEqual({
      budgetId: 'b1',
      categoryId: 'c-gift',
      amount: 300,
      warningThreshold: 90,
      validFrom: '2026-10-01',
    });
    request.flush({ id: 'l2', limit: 300, warningThreshold: 90, validFrom: '2026-10-01' });
    await saving;
  });

  it('przy ZMIANIE istniejącego limitu kalendarz blokuje minione miesiące, przy dodawaniu nie', async () => {
    // Pierwszy limit wolno ustawić wstecz — zablokowanie tego odcięłoby jedyną drogę do historii.
    await settle();

    api().openEditor(null, 'c-gift');
    expect(api().disabledMonth(new Date(2026, 5, 1))).toBe(false);

    api().openEditor(row());
    expect(api().disabledMonth(new Date(2026, 7, 1))).toBe(true);
    expect(api().disabledMonth(new Date(2026, 8, 1))).toBe(false);
  });

  it('usunięcie pyta czerwonym przyciskiem, podaje nowy limit budżetu i dopiero potem wysyła DELETE', async () => {
    await settle();

    const removing = api().remove(row());
    await Promise.resolve();

    expect(confirmDialog.lastOptions?.danger).toBe(true);
    expect(confirmDialog.lastOptions?.header).toBe('Usunąć limit „Jedzenie”?');
    expect(confirmDialog.lastOptions?.description).toBe('spadnie do 0,00 zł');
    http.expectNone((r) => r.method === 'DELETE');

    confirmDialog.respond(true);
    await Promise.resolve();
    await Promise.resolve();
    http.expectOne((r) => r.method === 'DELETE' && r.url === '/api/limits/l1').flush(null);
    await removing;
  });
});
