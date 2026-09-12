import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { APP_ICONS } from '../../core/icons';
import { CreateBudget } from './create-budget';

/**
 * Krok 1 kreatora budżetu. Testujemy zachowanie, nie renderowanie: czy „Zakończ"
 * jest odblokowane we właściwym momencie i co dokładnie leci do API.
 */
describe('CreateBudget', () => {
  let fixture: ComponentFixture<CreateBudget>;
  let component: CreateBudget;
  let http: HttpTestingController;

  /** Sygnały są `protected` — test sięga po nie tak, jak robi to szablon komponentu. */
  const api = () => component as unknown as {
    name: { set(v: string): void };
    currency: { set(v: string): void; (): string };
    initialBalance: { set(v: number): void; (): number };
    canFinish(): boolean;
    error(): string | null;
    finish(): Promise<void>;
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateBudget],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // Po zapisie komponent przechodzi na dashboard — bez tej trasy router rzuca
        // NG04002 już poza testem, więc błąd nie wywracałby żadnej asercji, tylko szumiał.
        provideRouter([{ path: 'dashboard', children: [] }]),
        provideNoopAnimations(),
        provideTranslateService(),
        // Bez tego `nz-icon` dociągałby `assets/outline/*.svg` przez HttpClient,
        // a nierozstrzygnięte żądanie zawiesza `whenStable()`.
        provideNzIcons(APP_ICONS),
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).use('pl');
    http = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(CreateBudget);
    component = fixture.componentInstance;
    fixture.detectChanges();

    // Słowniki formularza pobiera httpResource przy pierwszym renderze. Flush MUSI iść
    // przed `whenStable()` — wiszące żądanie nigdy nie pozwoliłoby się ustabilizować.
    http.match('/api/budgets/currencies').forEach((r) => r.flush([{ code: 'PLN' }]));
    await fixture.whenStable();
  });

  // Świadomie BEZ `http.verify()`: NG-ZORRO dociąga ikony (`assets/outline/*.svg`) tym samym
  // HttpClientem, więc verify raportowałby je jako niespodziewane żądania. Krotność wywołań
  // do API pilnuje `expectOne` w testach poniżej.

  it('blokuje „Zakończ", dopóki budżet nie ma nazwy', () => {
    expect(api().canFinish()).toBe(false);

    api().name.set('Domowy');
    expect(api().canFinish()).toBe(true);
  });

  it('nie uznaje samych spacji za nazwę', () => {
    // Inaczej powstalby budzet bez nazwy, ktorego nie da sie rozpoznac w selektorze.
    api().name.set('   ');
    expect(api().canFinish()).toBe(false);
  });

  it('waluta startuje na PLN, bo tak pokazuje makieta', () => {
    expect(api().currency()).toBe('PLN');
  });

  it('wysyła przyciętą nazwę, walutę i bilans początkowy', async () => {
    api().name.set('  Wrzesień  ');
    api().initialBalance.set(2500.5);

    const done = api().finish();
    const request = http.expectOne('/api/budgets');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Wrzesień',
      currency: 'PLN',
      initialBalance: 2500.5,
    });

    request.flush({ id: 'a1000000-0000-4000-8000-000000000001', name: 'Wrzesień', month: '2026-09-01', currency: 'PLN', initialBalance: 2500.5 });
    await done;
  });

  it('przepuszcza ujemny bilans początkowy', async () => {
    // Debet na koncie to legalny stan startowy — blokowanie go zmuszałoby
    // do wpisania nieprawdy, żeby w ogóle założyć budżet.
    api().name.set('Po remoncie');
    api().initialBalance.set(-1200);

    const done = api().finish();
    const request = http.expectOne('/api/budgets');

    expect(request.request.body.initialBalance).toBe(-1200);
    request.flush({ id: 'a1000000-0000-4000-8000-000000000002', name: 'Po remoncie', month: '2026-09-01', currency: 'PLN', initialBalance: -1200 });
    await done;
  });

  it('bilans startuje na zerze, więc nie blokuje zapisu', () => {
    api().name.set('Domowy');
    expect(api().initialBalance()).toBe(0);
    expect(api().canFinish()).toBe(true);
  });

  it('pokazuje komunikat z API, gdy zapis się nie uda', async () => {
    api().name.set('Domowy');

    const done = api().finish();
    // Backend trzyma teksty w zasobach i przysyla je gotowe — front ich nie sklada.
    http.expectOne('/api/budgets').flush(
      { error: 'Podaj nazwę budżetu.' },
      { status: 400, statusText: 'Bad Request' },
    );
    await done;

    expect(api().error()).toBe('Podaj nazwę budżetu.');
  });
});
