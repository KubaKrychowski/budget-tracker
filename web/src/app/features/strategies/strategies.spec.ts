import { registerLocaleData } from '@angular/common';
import pl from '@angular/common/locales/pl';
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
import { StrategiesResponse, StrategyListItem } from '../../core/api/models/strategies';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../core/confirm-dialog/confirm-dialog-options';
import { Strategies } from './strategies';

registerLocaleData(pl);

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

/** Dostęp do chronionych członków — testy sterują oknem i akcjami tak jak szablon. */
interface StrategiesApi {
  openCreate(template: 'Blank' | 'LoanAndCushion'): void;
  newName: { set(v: string): void };
  create(): Promise<void>;
  remove(row: StrategyListItem): Promise<void>;
  creating(): string | null;
}

/** Ekran „Wybierz strategię”. Strategie i ich liczniki liczy serwer — tu sprawdzamy, co ekran mówi i wysyła. */
describe('Strategies', () => {
  let fixture: ComponentFixture<Strategies>;
  let http: HttpTestingController;
  let confirmDialog: FakeConfirmDialogService;
  let router: Router;

  const item = (over: Partial<StrategyListItem> = {}): StrategyListItem => ({
    id: 's1',
    name: 'Kredyt i poduszka 2027',
    eventCount: 9,
    actionCount: 14,
    updatedAt: '2026-10-02T10:00:00+00:00',
    ...over,
  });

  const response = (over: Partial<StrategiesResponse> = {}): StrategiesResponse => ({
    selectedBudgetId: 'b1',
    budgets: [{ id: 'b1', name: 'Domowy', month: '2026-10-01', disabled: false }],
    strategies: [item()],
    ...over,
  });

  const api = (): StrategiesApi => fixture.componentInstance as unknown as StrategiesApi;
  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  const settle = async (body: StrategiesResponse = response()): Promise<void> => {
    fixture.detectChanges();
    http.match((r) => r.url === '/api/strategies' && r.method === 'GET').forEach((r) => r.flush(body));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [Strategies],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'dashboard', children: [] },
          { path: 'strategies', children: [] },
          { path: 'strategies/:id', children: [] },
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
      dashboard: { configuration: 'Konfiguracja', breadcrumb: 'Dashboard' },
      strategies: {
        title: 'Strategie',
        heading: 'Wybierz strategię',
        lead: 'Każda strategia to osobna tablica.',
        new: 'Nowa strategia',
        fromTemplate: 'Z szablonu: kredyt i poduszka',
        blank: 'Pusta tablica',
        emptyLead: 'Nie masz jeszcze żadnej strategii.',
        newLead: 'Zacznij od szablonu albo od pustej tablicy.',
        meta: 'Zdarzenia: {{events}} · akcje: {{actions}}',
        changed: 'zmieniona {{date}}',
        open: 'Otwórz',
        remove: 'Usuń',
        removed: 'Strategia usunięta.',
        removeConfirm: { header: 'Usunąć strategię „{{name}}”?', description: 'Zdarzenia: {{events}}, akcje: {{actions}}.' },
        noBudget: 'Utwórz najpierw budżet.',
        create: { title: 'Nowa strategia', nameLabel: 'Nazwa strategii', ok: 'Utwórz', cancel: 'Anuluj', templateNote: 'Szablon ma przykładowe liczby.', defaultNameLoan: 'Kredyt i poduszka', defaultNameBlank: 'Moja strategia' },
        errors: { loadFailed: 'Nie udało się wczytać strategii', saveFailed: 'Nie udało się zapisać zmian' },
      },
    });
    translate.use('pl');

    router = TestBed.inject(Router);
    await router.navigate(['/strategies']);
    fixture = TestBed.createComponent(Strategies);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
  });

  it('pyta o JEDEN budżet z widoku, gdy adres go nie podaje', async () => {
    TestBed.inject(ActiveBudget).set(['z-dashboardu']);
    fixture.detectChanges();

    const request = http.expectOne((r) => r.url === '/api/strategies');

    expect(request.request.params.get('budgetId')).toBe('z-dashboardu');
    request.flush(response());
  });

  it('pokazuje strategie z licznikami zdarzeń i akcji oraz odnośnikiem do tablicy', async () => {
    await settle();

    expect(text()).toContain('Kredyt i poduszka 2027');
    expect(text()).toContain('Zdarzenia: 9 · akcje: 14');
    const link = fixture.nativeElement.querySelector('a.str__row-name') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/strategies/s1');
  });

  it('bez strategii mówi, od czego zacząć, i oferuje szablon oraz pustą tablicę', async () => {
    await settle(response({ strategies: [] }));

    expect(text()).toContain('Nie masz jeszcze żadnej strategii.');
    expect(text()).toContain('Z szablonu: kredyt i poduszka');
    expect(text()).toContain('Pusta tablica');
  });

  it('bez żadnego budżetu nie pozwala zakładać strategii', async () => {
    await settle(response({ selectedBudgetId: null, budgets: [], strategies: [] }));

    expect(text()).toContain('Utwórz najpierw budżet.');
    expect(text()).not.toContain('Z szablonu: kredyt i poduszka');
  });

  it('zakłada strategię z szablonu w wybranym budżecie i przechodzi na jej tablicę', async () => {
    await settle();
    const navigate = vi.spyOn(router, 'navigate');

    api().openCreate('LoanAndCushion');
    expect(api().creating()).toBe('LoanAndCushion');
    api().newName.set('  Mój plan  ');
    const done = api().create();
    const post = http.expectOne((r) => r.url === '/api/strategies' && r.method === 'POST');
    expect(post.request.body).toEqual({ budgetId: 'b1', name: 'Mój plan', template: 'LoanAndCushion' });
    post.flush({ id: 'nowa' });
    await done;

    expect(navigate).toHaveBeenCalledWith(['/strategies', 'nowa']);
  });

  it('nie zakłada strategii bez nazwy', async () => {
    await settle();

    api().openCreate('Blank');
    api().newName.set('   ');
    await api().create();

    expect(http.match((r) => r.method === 'POST')).toHaveLength(0);
  });

  it('usuwa strategię dopiero po potwierdzeniu i odświeża listę', async () => {
    await settle();

    const removal = api().remove(item());
    expect(confirmDialog.lastOptions?.header).toBe('Usunąć strategię „Kredyt i poduszka 2027”?');
    expect(confirmDialog.lastOptions?.danger).toBe(true);
    confirmDialog.respond(true);
    await Promise.resolve();
    http.expectOne((r) => r.url === '/api/strategies/s1' && r.method === 'DELETE').flush(null);
    await removal;
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve));

    expect(http.match((r) => r.url === '/api/strategies' && r.method === 'GET').length).toBe(1);
  });

  it('nic nie usuwa, gdy użytkownik anuluje', async () => {
    await settle();

    const removal = api().remove(item());
    confirmDialog.respond(false);
    await removal;

    expect(http.match((r) => r.method === 'DELETE')).toHaveLength(0);
  });

  it('pokazuje błąd wczytania z możliwością ponowienia', async () => {
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/strategies').flush({}, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text()).toContain('Nie udało się wczytać strategii');
  });
});
