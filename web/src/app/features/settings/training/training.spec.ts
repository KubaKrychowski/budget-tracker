import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { APP_ICONS } from '../../../core/icons';
import { TrainingSetOverview } from '../../../core/api/models/training-set-overview';
import { ConfirmDialogService } from '../../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../../core/confirm-dialog/confirm-dialog-options';
import { Training } from './training';

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
 * Zakładka „Dane treningowe". Testujemy to, co niesie treść, a nie układ: rozbicie zbioru
 * na źródła, wyróżnienie kategorii z zerem przykładów i to, że przywrócenie modelu pyta,
 * zanim podmieni cokolwiek.
 */
describe('Training', () => {
  let fixture: ComponentFixture<Training>;
  let http: HttpTestingController;
  let confirmDialog: FakeConfirmDialogService;

  const overview = (over: Partial<TrainingSetOverview> = {}): TrainingSetOverview => ({
    composition: { fromFile: 1224, fromCorrections: 43, corrected: 11, duplicates: 36, total: 1267 },
    categories: [
      { name: 'Jedzenie', count: 437 },
      { name: 'Wyposażenie domu', count: 1 },
      { name: 'Wynagrodzenie', count: 0 },
      { name: 'Zwroty', count: 0 },
    ],
    pendingReview: 69,
    correctionsSinceLastTraining: 43,
    models: [
      {
        version: '20260906045531',
        createdAt: '2026-09-06T04:55:31+00:00',
        isActive: true,
        report: { rows: 1267, categories: 25, microAccuracy: 0.9076, macroAccuracy: 0.8466 },
      },
      {
        version: '20260902043132',
        createdAt: '2026-09-02T04:31:32+00:00',
        isActive: false,
        report: null,
      },
    ],
    metricsAreIndicative: true,
    ...over,
  });

  const flush = (body: TrainingSetOverview = overview()): void => {
    http.match('/api/categorization/training-set').forEach((r) => r.flush(body));
  };

  const text = (): string =>
    (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  /** Przycisk „Rozumiem" przy zastrzeżeniu o metrykach. */
  const ack = () =>
    fixture.debugElement.queryAll(By.css('button'))
      .find((b) => (b.nativeElement.textContent as string).includes('Rozumiem'))!;

  const settle = async (body?: TrainingSetOverview): Promise<void> => {
    fixture.detectChanges();
    flush(body);
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    // Zamknięcie zastrzeżenia jest PAMIĘTANE między wizytami — bez czyszczenia jeden test chowałby je reszcie.
    localStorage.clear();
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [Training],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'transactions', children: [] }]),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
        provideNzI18n(pl_PL),
        { provide: ConfirmDialogService, useValue: confirmDialog },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      settings: {
        training: {
          caption: 'Dane treningowe',
          composition: {
            fromFile: 'Z pliku bazowego',
            fromCorrections: 'Z Twoich poprawek',
            total: 'Razem w zbiorze',
            corrected: '{{count}} Twoich poprawek zmieniło kategorię przykładom w zbiorze.',
            duplicates: 'Pominięto {{count}} poprawek, które są już w pliku bazowym.',
          },
          newCorrections: 'Od ostatniego treningu przybyło {{count}} poprawek.',
          upToDate: 'Model uczył się na wszystkich dotychczasowych poprawkach.',
          train: 'Doucz model',
          trainSuccess: 'Model douczony na {{rows}} przykładach.',
          report: {
            title: 'Trening zakończony',
            body: '{{rows}} przykładów, {{categories}} kategorii. {{micro}} / {{macro}}.',
          },
          metricsCaveat: 'Trafności są orientacyjne, nie są bramką jakości.',
          metricsCaveatAck: 'Rozumiem',
          pendingReview: 'W kolejce do przeglądu czeka {{count}} transakcji.',
          goToReview: 'Przejrzyj je',
          distribution: {
            caption: 'Rozkład po kategoriach',
            category: 'Kategoria',
            examples: 'Przykładów',
            share: 'Udział',
            emptyWarning: '{{count}} kategorii nie ma ani jednego przykładu.',
          },
          history: {
            caption: 'Historia treningów',
            trainedAt: 'Data treningu',
            rows: 'Przykładów',
            micro: 'Trafność',
            macro: 'Uśredniona',
            state: 'Stan',
            active: 'Aktywny',
            restore: 'Przywróć',
            empty: 'Nie było jeszcze żadnego treningu.',
          },
          restoreConfirm: {
            header: 'Przywrócić tę wersję modelu?',
            description: 'Aktywny model zostanie podmieniony.',
          },
          restoreSuccess: 'Przywrócono wskazaną wersję modelu.',
          recategorize: 'Przelicz kategorie',
          recategorizeSuccess: 'Przeliczono {{examined}} transakcji, kategorię zmieniło {{recategorized}}.',
          recategorizeConfirm: {
            header: 'Przeliczyć kategorie istniejących transakcji?',
            description: 'Kategorie nadane ręcznie i potwierdzone zostaną nietknięte.',
          },
          recategorizeReport: {
            title: 'Kategorie przeliczone',
            body: 'Sprawdzono {{examined}} transakcji: {{recategorized}} zmieniło kategorię, '
              + 'z czego {{movedToReview}} wróciło do kolejki przeglądu.',
          },
          errors: { network: 'Nie udało się połączyć z serwerem.' },
        },
      },
    });
    translate.use('pl');

    fixture = TestBed.createComponent(Training);
    http = TestBed.inject(HttpTestingController);
    await settle();
  });

  afterEach(() => {
    localStorage.clear();
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
  });

  it('rozbija zbiór na plik bazowy i poprawki użytkownika', () => {
    // To jest sens tej zakładki: odpowiedź na „czy moje poprawki w ogóle się liczą".
    expect(text()).toContain('1224');
    expect(text()).toContain('43');
    expect(text()).toContain('1267');
  });

  it('tłumaczy, dlaczego suma nie jest zwykłym dodawaniem', () => {
    // Bez tego „1224 + 43 = 1267" przy 79 poprawkach w bazie wygląda na błąd arytmetyczny.
    expect(text()).toContain('Pominięto 36 poprawek');
  });

  it('wyróżnia kategorie bez ani jednego przykładu', () => {
    // Zero to inny stan jakościowy niż „mało": model nigdy takiej kategorii nie wskaże.
    expect(text()).toContain('2 kategorii nie ma ani jednego przykładu');
    expect(text()).toContain('Wynagrodzenie');
    expect(text()).toContain('Zwroty');
  });

  it('pokazuje zastrzeżenie o metrykach, dopóki użytkownik go nie zamknie', () => {
    // Metryki nie są bramką jakości — każdy trening dzieli inny zbiór, bo zbiór rośnie.
    expect(text()).toContain('orientacyjne');
  });

  it('„Rozumiem" chowa zastrzeżenie i pamięta to po powrocie na ekran', async () => {
    ack().nativeElement.click();
    fixture.detectChanges();
    expect(text()).not.toContain('orientacyjne');

    // Sedno: gdyby zamknięcie żyło tylko w komponencie, to samo ostrzeżenie wracałoby
    // przy każdym wejściu w zakładkę — a mówi ono o stałej właściwości metryk, nie o zdarzeniu.
    fixture = TestBed.createComponent(Training);
    await settle();
    expect(text()).not.toContain('orientacyjne');
  });

  it('zachęca liczbą nowych poprawek, zamiast odpalać trening sam', () => {
    expect(text()).toContain('przybyło 43 poprawek');
    http.expectNone('/api/categorization/train');
  });

  it('mówi wprost, gdy model widział już wszystkie poprawki', async () => {
    // Świeży komponent, nie `settle` z inną odpowiedzią: `httpResource` odpytuje serwer
    // dopiero, gdy zmienią się parametry, a tu adres jest stały.
    fixture = TestBed.createComponent(Training);
    await settle(overview({ correctionsSinceLastTraining: 0 }));

    expect(text()).toContain('uczył się na wszystkich');
  });

  it('pokazuje raport po zakończonym treningu', async () => {
    const component = fixture.componentInstance as unknown as { train(): Promise<void> };
    void component.train();
    await fixture.whenStable();

    http.expectOne('/api/categorization/train').flush({
      rows: 1300, categories: 25, microAccuracy: 0.91, macroAccuracy: 0.85,
    });
    await settle();

    expect(text()).toContain('1300 przykładów, 25 kategorii');
    expect(text()).toContain('91%');
    expect(text()).toContain('85%');
  });

  it('pyta przed przywróceniem i nie rusza modelu po odmowie', async () => {
    const component = fixture.componentInstance as unknown as {
      restore(m: { version: string }): Promise<void>;
    };
    void component.restore({ version: '20260902043132' });
    await fixture.whenStable();

    expect(confirmDialog.lastOptions?.header).toContain('Przywrócić');

    confirmDialog.respond(false);
    await fixture.whenStable();

    http.expectNone('/api/categorization/activate');
  });

  it('przywraca wskazaną wersję po potwierdzeniu', async () => {
    const component = fixture.componentInstance as unknown as {
      restore(m: { version: string }): Promise<void>;
    };
    void component.restore({ version: '20260902043132' });
    await fixture.whenStable();
    confirmDialog.respond(true);
    await fixture.whenStable();

    const request = http.expectOne('/api/categorization/activate');
    expect((request.request.body as { version: string }).version).toBe('20260902043132');
    request.flush(null);
    await settle();
  });

  it('model bez historii pokazuje kreski zamiast zmyślonych metryk', () => {
    // Wersja sprzed wersjonowania: plik jest, ale nie wiadomo, na czym się uczyła.
    const rows = fixture.nativeElement.querySelectorAll('nz-table')[1]
      ?.querySelectorAll('tbody tr');
    expect(rows[1].textContent).toContain('—');
  });

  it('pyta przed przeliczeniem kategorii i nie rusza danych po odmowie', async () => {
    // Przeliczenie zmienia dane, na które użytkownik w tej chwili nie patrzy — musi być
    // moment, w którym można się nie zgodzić.
    const component = fixture.componentInstance as unknown as { recategorize(): Promise<void> };
    void component.recategorize();
    await fixture.whenStable();

    expect(confirmDialog.lastOptions?.header).toContain('Przeliczyć');

    confirmDialog.respond(false);
    await fixture.whenStable();

    http.expectNone('/api/categorization/recategorize');
  });

  it('po przeliczeniu mówi, ile wierszy wróciło do kolejki przeglądu', async () => {
    // Sam komunikat „gotowe" byłby prośbą o zaufanie: zmiana dotyczy danych poza ekranem,
    // a wzrost kolejki to jedyny skutek, który dokłada użytkownikowi pracy.
    const component = fixture.componentInstance as unknown as { recategorize(): Promise<void> };
    void component.recategorize();
    await fixture.whenStable();
    confirmDialog.respond(true);
    await fixture.whenStable();

    http.expectOne('/api/categorization/recategorize').flush({
      examined: 1288, recategorized: 40, movedToReview: 12, unchanged: 1248,
    });
    await settle();

    expect(text()).toContain('Sprawdzono 1288 transakcji');
    expect(text()).toContain('12 wróciło do kolejki przeglądu');
  });

  it('pokazuje poprawki, które zmieniły etykietę przykładom już obecnym w zbiorze', () => {
    // Te przykłady nie powiększają zbioru, więc nie widać ich w żadnym z trzech kafli —
    // a to właśnie one niosą „model nauczy się czegoś INNEGO". Wcześniej były po cichu
    // wyrzucane razem z duplikatami i nie zmieniały w modelu niczego.
    expect(text()).toContain('11 Twoich poprawek zmieniło kategorię');
  });
});
