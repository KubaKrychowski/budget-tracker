import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { APP_ICONS } from '../icons';
import { Terminal } from './terminal';
import { TerminalService } from './terminal.service';

/**
 * Panel terminala (issue #25) — `nz-drawer` renderuje treść w overlayu poza hostem tego
 * komponentu (CDK `Portal`), więc czytamy `document.body`, tym samym wzorcem co modale
 * NG-ZORRO (`web/CLAUDE.md`).
 */
describe('Terminal', () => {
  let fixture: ComponentFixture<Terminal>;
  let service: TerminalService;
  let http: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [Terminal],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
      ],
    }).compileComponents();

    service = TestBed.inject(TerminalService);
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Terminal);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush({}); });
    http.verify({ ignoreCancelled: true });
    sessionStorage.clear();
  });

  const bodyText = () => document.body.textContent ?? '';

  const typeAndSubmit = async (line: string) => {
    const input = document.body.querySelector('.term__input') as HTMLInputElement;
    input.value = line;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  it('domyślnie jest zamknięty', () => {
    expect(service.isOpen()).toBe(false);
  });

  it('TerminalService.open() otwiera panel z podpowiedzią „help"', () => {
    service.open();
    fixture.detectChanges();

    expect(bodyText()).toContain('terminal.hint');
  });

  it('klik na X zamyka panel przez TerminalService', () => {
    service.open();
    fixture.detectChanges();

    (document.body.querySelector('.term__close') as HTMLButtonElement).click();

    expect(service.isOpen()).toBe(false);
  });

  it('wysyła linię do /api/cli/execute i pokazuje wynik w scrollbacku', async () => {
    service.open();
    fixture.detectChanges();

    await typeAndSubmit('budget list');

    const req = http.expectOne((r) => r.url === '/api/cli/execute');
    expect(req.request.body).toEqual({ line: 'budget list' });
    req.flush({ budgets: [], retentionDays: 30 });
    await fixture.whenStable();
    fixture.detectChanges();

    const text = bodyText();
    expect(text).toContain('budget list');
    expect(text).toContain('retentionDays');
  });

  it('błąd z API (400) pokazuje komunikat z pola error, nie surowy JSON', async () => {
    service.open();
    fixture.detectChanges();

    await typeAndSubmit('budget disable nieznany-id');

    const req = http.expectOne((r) => r.url === '/api/cli/execute');
    req.flush({ error: 'Nieznany --budget-id.' }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(bodyText()).toContain('Nieznany --budget-id.');
  });

  it('strzałka w górę przywraca ostatnio wpisaną komendę', async () => {
    service.open();
    fixture.detectChanges();
    await typeAndSubmit('budget list');
    http.expectOne((r) => r.url === '/api/cli/execute').flush({});
    await fixture.whenStable();
    fixture.detectChanges();

    // `*nzDrawerContent` renderuje przez CDK Portal poza drzewem komponentu — samo
    // `detectChanges()` fixture'a nie zawsze dociera do portalowanej treści (patrz
    // web/CLAUDE.md: „po router.navigate whenStable potrafi zawisnąć" — ten sam rodzaj
    // niedopasowania cyklu CD, tu przy portalu zamiast routera).
    (fixture.componentInstance as unknown as { historyUp(): void }).historyUp();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const input = document.body.querySelector('.term__input') as HTMLInputElement;
    expect(input.value).toBe('budget list');
  });

  it('pusta linia nie wysyła żądania', async () => {
    service.open();
    fixture.detectChanges();

    await typeAndSubmit('   ');

    http.expectNone((r) => r.url === '/api/cli/execute');
  });
});
