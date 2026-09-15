import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { provideMarkdown } from 'ngx-markdown';
import { APP_ICONS } from '../../core/icons';
import { HANDBOOK_TOPICS } from '../../core/handbook-topics';
import { Handbook } from './handbook';

/**
 * Podręcznik (issue #19) — lista tematów po lewej, treść z `public/handbook/*.md` po prawej,
 * wybór w adresie (`?topic=`), wyszukiwarka po tytule i po treści wszystkich tematów.
 * Testujemy dopasowanie adresu/zapytania na plik i na podświetlenie w liście, nie samą treść
 * markdown (to odpowiedzialność `ngx-markdown`/`marked`, nie nasza).
 */
describe('Handbook', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'handbook', component: Handbook }]),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
        provideMarkdown(),
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush(''); });
    http.verify({ ignoreCancelled: true });
  });

  // `MarkdownComponent.render()` jest asynchroniczne (marked, sanitizer) i nie trzyma żadnego
  // zadania, na które czekałby `whenStable()` — bez krótkiego odczekania w mikrokolejce test
  // sprawdzałby DOM, zanim ngx-markdown zdąży wstawić wynik.
  const flushMicrotasks = () => new Promise((resolve) => setTimeout(resolve));

  /**
   * Ekran ładuje treść wybranego tematu (do wyświetlenia) I treść WSZYSTKICH tematów naraz
   * (do wyszukiwania) — stąd naraz może wisieć nawet 10 żądań do 9 plików. Jedno wywołanie
   * odpowiada na wszystkie pending naraz: podane URL-e dostają swoją treść, reszta pustą.
   */
  const flushHandbookFiles = (contentByUrl: Record<string, string> = {}) => {
    http.match(() => true).forEach((r) => r.flush(contentByUrl[r.request.url] ?? ''));
  };

  it('bez ?topic= w adresie pokazuje pierwszy temat z listy', async () => {
    const harness = await RouterTestingHarness.create('/handbook');
    harness.detectChanges();

    flushHandbookFiles({ 'handbook/dashboard.md': '## Pulpit i podsumowanie' });
    await flushMicrotasks();
    harness.detectChanges();

    const html = harness.routeNativeElement?.innerHTML ?? '';
    expect(html).toContain('Pulpit i podsumowanie');
  });

  it('?topic= wczytuje właściwy plik i podświetla właściwą pozycję w liście', async () => {
    const harness = await RouterTestingHarness.create('/handbook?topic=savings');
    harness.detectChanges();

    flushHandbookFiles({ 'handbook/savings.md': '## Cele oszczędzania i rezerwacje' });
    await flushMicrotasks();
    harness.detectChanges();

    const html = harness.routeNativeElement?.innerHTML ?? '';
    expect(html).toContain('Cele oszczędzania i rezerwacje');

    const selected = harness.routeNativeElement?.querySelector('.hb__topic--selected');
    expect(selected?.textContent?.trim()).toBe('handbook.topics.savings');
  });

  it('nieznany temat w adresie wraca do pierwszego z listy', async () => {
    const harness = await RouterTestingHarness.create('/handbook?topic=nieistniejacy');
    harness.detectChanges();

    flushHandbookFiles({ 'handbook/dashboard.md': '## Pulpit i podsumowanie' });
    await flushMicrotasks();
    harness.detectChanges();

    const selected = harness.routeNativeElement?.querySelector('.hb__topic--selected');
    expect(selected?.textContent?.trim()).toBe('handbook.topics.dashboard');
  });

  describe('wyszukiwarka', () => {
    const search = (harness: RouterTestingHarness, phrase: string) => {
      const input = harness.routeNativeElement!.querySelector('input') as HTMLInputElement;
      input.value = phrase;
      input.dispatchEvent(new Event('input'));
      harness.detectChanges();
    };

    const topicLabels = (harness: RouterTestingHarness) =>
      [...harness.routeNativeElement!.querySelectorAll('.hb__topic')].map((el) => el.textContent?.trim());

    it('filtruje listę po tytule tematu, bez uwzględniania wielkości liter', async () => {
      const harness = await RouterTestingHarness.create('/handbook');
      harness.detectChanges();
      flushHandbookFiles();
      await flushMicrotasks();
      harness.detectChanges();

      // Bez tłumaczeń w teście `translate.instant` zwraca sam klucz — „savings" jest
      // podciągiem WYŁĄCZNIE `handbook.topics.savings`, więc to jednoznaczne zapytanie.
      search(harness, 'SAVINGS');

      expect(topicLabels(harness)).toEqual(['handbook.topics.savings']);
    });

    it('filtruje listę też po treści tematu, nie tylko po tytule', async () => {
      const harness = await RouterTestingHarness.create('/handbook');
      harness.detectChanges();
      flushHandbookFiles({ 'handbook/transactions.md': 'Rozpoznaje sprzedawcę „Kaczka i Spółka".' });
      await flushMicrotasks();
      harness.detectChanges();

      search(harness, 'kaczka');

      expect(topicLabels(harness)).toEqual(['handbook.topics.transactions']);
    });

    it('brak dopasowania pokazuje komunikat zamiast pustej listy', async () => {
      const harness = await RouterTestingHarness.create('/handbook');
      harness.detectChanges();
      flushHandbookFiles();
      await flushMicrotasks();
      harness.detectChanges();

      search(harness, 'cos-czego-na-pewno-nie-ma-w-podreczniku');

      expect(topicLabels(harness)).toEqual([]);
      expect(harness.routeNativeElement?.querySelector('.hb__no-results')?.textContent?.trim())
        .toBe('handbook.noResults');
    });

    it('puste zapytanie pokazuje z powrotem wszystkie tematy', async () => {
      const harness = await RouterTestingHarness.create('/handbook');
      harness.detectChanges();
      flushHandbookFiles();
      await flushMicrotasks();
      harness.detectChanges();

      search(harness, 'savings');
      search(harness, '');

      expect(topicLabels(harness).length).toBe(HANDBOOK_TOPICS.length);
    });
  });
});
