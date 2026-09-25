import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { APP_ICONS } from '../icons';
import { ActiveBudget } from '../active-budget';
import { SearchResponse } from '../api/models/search';
import { RecentScreens } from '../search/recent-screens';
import { SearchHistory } from '../search/search-history';
import { SearchMenu } from './search-menu';

/**
 * Menu wyszukiwarki z nagłówka. Sprawdzamy to, co ekran MÓWI i dokąd prowadzi —
 * nie stan sygnałów.
 */
describe('SearchMenu', () => {
  let fixture: ComponentFixture<SearchMenu>;
  let http: HttpTestingController;

  const response = (over: Partial<SearchResponse> = {}): SearchResponse => ({
    query: 'catering',
    groups: [
      {
        kind: 'transactions',
        total: 21,
        hits: [
          {
            id: 't1',
            label: 'CATERING PUDELKOWY',
            categoryName: 'Catering',
            date: '2026-08-27',
            amount: -1200,
            budgetId: 'b1',
            budgetName: 'Budżet Główny',
          },
        ],
      },
    ],
    total: 21,
    budgetId: 'b1',
    budgetName: 'Budżet Główny',
    allBudgets: false,
    ...over,
  });

  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  const input = (): HTMLInputElement => fixture.nativeElement.querySelector('input') as HTMLInputElement;

  const openMenu = (): void => {
    input().dispatchEvent(new Event('focus'));
    fixture.detectChanges();
  };

  /** Odstęp na debounce wyszukiwarki — bez niego żądanie jeszcze nie wyszło. */
  const afterDebounce = () => new Promise((resolve) => setTimeout(resolve, 260));

  const type = async (value: string): Promise<void> => {
    const el = input();
    el.value = value;
    el.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await afterDebounce();
    fixture.detectChanges();
  };

  const answer = async (body: SearchResponse = response()): Promise<void> => {
    http.match((r) => r.url === '/api/search').forEach((r) => r.flush(body));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SearchMenu],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'transactions', children: [] },
          { path: 'limits', children: [] },
          { path: 'dashboard', children: [] },
        ]),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      dashboard: { currency: 'PLN' },
      actions: { limits: 'Limity wydatków', importStatement: 'Import wyciągu', addTransaction: 'Dodaj transakcje' },
      search: {
        placeholder: 'Szukaj',
        loading: 'Szukam…',
        showAll: 'Pokaż wszystkie wyniki ({{count}})',
        recentScreens: 'Ostatnie ekrany',
        firstSteps: 'Od czego zacząć',
        history: { title: 'Historia wyszukiwań', clear: 'Wyczyść', empty: 'Pusto — historia zbiera się sama.' },
        groups: { actions: 'Akcje', transactions: 'Transakcje', categories: 'Kategorie' },
        empty: {
          title: 'Nic nie znaleziono dla „{{query}}”',
          body: 'Sprawdź pisownię albo poszerz zakres — szukam tylko w budżecie {{budget}}.',
          bodyAll: 'Sprawdź pisownię — szukałem już we wszystkich budżetach.',
        },
        scope: { hint: 'Szukam w budżecie wybranym na dashboardzie', searchAll: 'Szukaj we wszystkich budżetach', backToBudget: 'Wróć do jednego budżetu' },
      },
    });
    translate.use('pl');

    fixture = TestBed.createComponent(SearchMenu);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
  });

  // ── Menu bez frazy ───────────────────────────────────────────────────────────────────

  it('menu otwiera się po kliknięciu w PUSTE pole, bez żadnego żądania', () => {
    openMenu();

    expect(text()).toContain('Historia wyszukiwań');
    expect(http.match((r) => r.url === '/api/search')).toHaveLength(0);
  });

  it('bez historii tłumaczy, skąd się weźmie, i podsuwa pierwsze kroki', () => {
    openMenu();

    expect(text()).toContain('Pusto — historia zbiera się sama.');
    expect(text()).toContain('Od czego zacząć');
  });

  it('odwiedzone ekrany pokazują się jako siatka „Ostatnie ekrany"', () => {
    TestBed.inject(RecentScreens).track('/limits');
    openMenu();

    expect(text()).toContain('Ostatnie ekrany');
    expect(text()).toContain('Limity wydatków');
    expect(text()).not.toContain('Od czego zacząć');
  });

  it('„Wyczyść" opróżnia historię', () => {
    TestBed.inject(SearchHistory).add('catering');
    openMenu();
    expect(text()).toContain('catering');

    const clear = [...fixture.nativeElement.querySelectorAll('button')]
      .find((b) => (b as HTMLElement).textContent?.includes('Wyczyść')) as HTMLButtonElement;
    clear.click();
    fixture.detectChanges();

    expect(text()).toContain('Pusto — historia zbiera się sama.');
  });

  // ── Wyniki ───────────────────────────────────────────────────────────────────────────

  it('pyta o wyniki dopiero od dwóch znaków i niesie budżet z widoku', async () => {
    TestBed.inject(ActiveBudget).set(['b1']);
    openMenu();

    await type('c');
    expect(http.match((r) => r.url === '/api/search')).toHaveLength(0);

    await type('catering');
    const [request] = http.match((r) => r.url === '/api/search');
    expect(request.request.params.get('q')).toBe('catering');
    expect(request.request.params.get('budgetId')).toBe('b1');
    request.flush(response());
  });

  it('nagłówek grupy podaje CAŁKOWITĄ liczbę trafień, nie liczbę widocznych wierszy', async () => {
    openMenu();
    await type('catering');
    await answer();

    // Jeden wiersz w podglądzie, dwadzieścia jeden w bazie — inaczej użytkownik przestaje szukać.
    expect(text()).toContain('Transakcje(21)');
    expect(text()).toContain('CATERING PUDELKOWY');
    expect(text()).toContain('Pokaż wszystkie wyniki (21)');
  });

  it('wiersz transakcji niesie kategorię, datę i kwotę', async () => {
    openMenu();
    await type('catering');
    await answer();

    expect(text()).toContain('Catering · 27.08.2026 · Budżet Główny');
    // Intl z pl-PL NIE grupuje liczb czterocyfrowych — patrz web/CLAUDE.md.
    expect(text()).toContain('1200,00 PLN');
  });

  it('brak wyników mówi, W KTÓRYM budżecie szukaliśmy', async () => {
    // Bez tego zdania pusty wynik wygląda jak awaria, a nie jak zawężony zakres.
    openMenu();
    await type('biedronkaa');
    await answer(response({ query: 'biedronkaa', groups: [], total: 0 }));

    expect(text()).toContain('Nic nie znaleziono dla „biedronkaa”');
    expect(text()).toContain('szukam tylko w budżecie Budżet Główny');
  });

  it('odpowiedź do STARSZEJ frazy nie jest pokazywana', async () => {
    openMenu();
    await type('catering');
    await answer(response({ query: 'cater' }));

    expect(text()).not.toContain('CATERING PUDELKOWY');
  });

  // ── Przejścia ────────────────────────────────────────────────────────────────────────

  it('wybór transakcji prowadzi na listę transakcji z wypełnionym filtrem', async () => {
    const router = TestBed.inject(Router);
    openMenu();
    await type('catering');
    await answer();

    const row = [...fixture.nativeElement.querySelectorAll('button')]
      .find((b) => (b as HTMLElement).textContent?.includes('CATERING PUDELKOWY')) as HTMLButtonElement;
    row.click();
    await new Promise((resolve) => setTimeout(resolve));

    const params = router.parseUrl(router.url).queryParams;
    expect(router.url).toContain('/transactions');
    expect(params['search']).toBe('CATERING PUDELKOWY');
    expect(params['budgetId']).toBe('b1');
  });

  it('wybór zapisuje frazę w historii, więc następne otwarcie ją proponuje', async () => {
    openMenu();
    await type('catering');
    await answer();

    const row = [...fixture.nativeElement.querySelectorAll('button')]
      .find((b) => (b as HTMLElement).textContent?.includes('CATERING PUDELKOWY')) as HTMLButtonElement;
    row.click();
    await new Promise((resolve) => setTimeout(resolve));

    expect(TestBed.inject(SearchHistory).entries()).toContain('catering');
  });
});
