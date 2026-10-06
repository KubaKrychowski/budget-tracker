import { registerLocaleData } from '@angular/common';
import pl from '@angular/common/locales/pl';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { vi } from 'vitest';
import { APP_ICONS } from '../../../core/icons';
import { AppUpdateService } from '../../../core/app-update/app-update.service';
import { AboutApp } from './about-app';

registerLocaleData(pl);

const ReleaseUrl = 'https://api.github.com/repos/KubaKrychowski/budget-tracker/releases/latest';

describe('Ustawienia → O aplikacji', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [AboutApp],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideTranslateService(), provideNzIcons(APP_ICONS)],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      settings: {
        about: {
          installed: 'Zainstalowana wersja', latest: 'Najnowsza wersja', checkedAt: 'Ostatnie sprawdzenie',
          check: 'Sprawdź aktualizacje', checkAgain: 'Sprawdź ponownie', retry: 'Spróbuj ponownie', download: 'Pobierz wersję {{latest}}',
          upToDate: { title: 'Masz najnowszą wersję', body: 'Wersja {{version}} jest aktualna.' },
          available: { title: 'Jest nowa wersja aplikacji', body: 'Wersja {{latest}} jest gotowa.' },
          error: { title: 'Nie udało się sprawdzić aktualizacji', body: 'Sprawdź połączenie.' },
        },
      },
    });
    translate.use('pl');
    http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(AppUpdateService) as unknown as { installedVersion: () => Promise<string> }, 'installedVersion').mockResolvedValue('2.2');
  });

  afterEach(() => vi.restoreAllMocks());

  const text = (el: HTMLElement) => el.textContent?.replace(/\s+/g, ' ') ?? '';
  /** Wartość z wiersza karty wersji (`dt` → `dd`), bo `textContent` skleja je bez spacji. */
  const value = (el: HTMLElement, label: string): string => {
    const row = [...el.querySelectorAll('.abt__row')].find((r) => r.querySelector('dt')?.textContent?.includes(label));
    return row?.querySelector('dd')?.textContent?.trim() ?? '';
  };
  const click = (el: HTMLElement, label: string) => (
    [...el.querySelectorAll('button')].find((b) => b.textContent?.includes(label)) as HTMLButtonElement
  ).click();

  const mount = async () => {
    const fixture = TestBed.createComponent(AboutApp);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  };

  /** Odpowiedź GitHuba: tag wydania albo kod błędu HTTP. */
  const answer = async (fixture: { detectChanges(): void }, tagOrStatus: string | number) => {
    await Promise.resolve();
    const req = http.expectOne(ReleaseUrl);
    if (typeof tagOrStatus === 'number') req.flush({}, { status: tagOrStatus, statusText: 'Error' });
    else req.flush({ tag_name: tagOrStatus, assets: [] });
    await new Promise((r) => setTimeout(r));
    fixture.detectChanges();
  };

  it('przed sprawdzeniem pokazuje zainstalowaną wersję i przycisk, bez alertu', async () => {
    const fixture = await mount();
    const el = fixture.nativeElement as HTMLElement;

    expect(value(el, 'Zainstalowana wersja')).toBe('2.2');
    expect(text(el)).toContain('Sprawdź aktualizacje');
    expect(el.querySelector('nz-alert')).toBeNull();
  });

  it('„Sprawdź aktualizacje” pyta GitHuba od razu, także gdy sprawdzano przed chwilą', async () => {
    localStorage.setItem('bt-app-update.checked-at', String(Date.now() - 1000));
    const fixture = await mount();

    click(fixture.nativeElement, 'Sprawdź aktualizacje');
    await answer(fixture, 'android-v2.2');

    expect(text(fixture.nativeElement)).toContain('Masz najnowszą wersję');
    expect(text(fixture.nativeElement)).toContain('Wersja 2.2 jest aktualna.');
  });

  it('nowa wersja: alert, podświetlona wersja i przycisk pobrania z numerem', async () => {
    const fixture = await mount();

    click(fixture.nativeElement, 'Sprawdź aktualizacje');
    await answer(fixture, 'android-v2.3');

    expect(text(fixture.nativeElement)).toContain('Jest nowa wersja aplikacji');
    expect(value(fixture.nativeElement, 'Najnowsza wersja')).toBe('2.3');
    expect(text(fixture.nativeElement)).toContain('Pobierz wersję 2.3');
    expect(text(fixture.nativeElement)).toContain('Sprawdź ponownie');
  });

  it('błąd sieci: czerwony alert i „Spróbuj ponownie” zamiast cichego braku wyniku', async () => {
    const fixture = await mount();

    click(fixture.nativeElement, 'Sprawdź aktualizacje');
    await answer(fixture, 500);

    expect(text(fixture.nativeElement)).toContain('Nie udało się sprawdzić aktualizacji');
    expect(text(fixture.nativeElement)).toContain('Spróbuj ponownie');
  });
});
