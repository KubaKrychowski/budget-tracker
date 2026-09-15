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
import { Handbook } from './handbook';

/**
 * Podręcznik (issue #19) — lista tematów po lewej, treść z `public/handbook/*.md` po prawej,
 * wybór w adresie (`?topic=`). Testujemy dopasowanie adresu na plik i na podświetlenie
 * w liście, nie samą treść markdown (to odpowiedzialność `ngx-markdown`/`marked`, nie nasza).
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
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush('treść'); });
    http.verify({ ignoreCancelled: true });
  });

  // `MarkdownComponent.render()` jest asynchroniczne (marked, sanitizer) i nie trzyma żadnego
  // zadania, na które czekałby `whenStable()` — bez krótkiego odczekania w mikrokolejce test
  // sprawdzałby DOM, zanim ngx-markdown zdąży wstawić wynik.
  const flushMicrotasks = () => new Promise((resolve) => setTimeout(resolve));

  it('bez ?topic= w adresie pokazuje pierwszy temat z listy', async () => {
    const harness = await RouterTestingHarness.create('/handbook');
    harness.detectChanges();

    const req = http.expectOne('handbook/dashboard.md');
    req.flush('## Pulpit i podsumowanie');
    await flushMicrotasks();
    harness.detectChanges();

    const html = harness.routeNativeElement?.innerHTML ?? '';
    expect(html).toContain('Pulpit i podsumowanie');
  });

  it('?topic= wczytuje właściwy plik i podświetla właściwą pozycję w liście', async () => {
    const harness = await RouterTestingHarness.create('/handbook?topic=savings');
    harness.detectChanges();

    const req = http.expectOne('handbook/savings.md');
    req.flush('## Cele oszczędzania i rezerwacje');
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

    http.expectOne('handbook/dashboard.md').flush('## Pulpit i podsumowanie');
    await harness.fixture.whenStable();
    harness.detectChanges();

    const selected = harness.routeNativeElement?.querySelector('.hb__topic--selected');
    expect(selected?.textContent?.trim()).toBe('handbook.topics.dashboard');
  });
});
