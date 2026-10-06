import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { vi } from 'vitest';
import { APP_ICONS } from '../icons';
import { AppUpdateBanner } from './app-update-banner';
import { AppUpdateService, isNewerVersion } from './app-update.service';

const ReleaseUrl = 'https://api.github.com/repos/KubaKrychowski/budget-tracker/releases/latest';

describe('isNewerVersion', () => {
  it('porównuje człony jako liczby, nie jako tekst', () => {
    expect(isNewerVersion('2.10', '2.9')).toBe(true);
    expect(isNewerVersion('2.9', '2.10')).toBe(false);
  });

  it('brakujący człon to zero, a ta sama wersja nie jest nowsza', () => {
    expect(isNewerVersion('2.1', '2.1.0')).toBe(false);
    expect(isNewerVersion('2.1.1', '2.1')).toBe(true);
    expect(isNewerVersion('2.1', '2.1')).toBe(false);
  });
});

describe('Pasek aktualizacji aplikacji', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [AppUpdateBanner],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslateService(), provideNzIcons(APP_ICONS)],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('pl', {
      appUpdate: { title: 'Jest nowa wersja aplikacji', versions: '{{latest}} · masz {{current}}', update: 'Zaktualizuj', dismiss: 'Zamknij' },
    });
    TestBed.inject(TranslateService).use('pl');
    http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(AppUpdateService) as unknown as { installedVersion: () => Promise<string> }, 'installedVersion')
      .mockResolvedValue('2.1');
  });

  afterEach(() => vi.restoreAllMocks());

  const render = async (tag: string) => {
    const fixture = TestBed.createComponent(AppUpdateBanner);
    const checking = TestBed.inject(AppUpdateService).check(true);
    await Promise.resolve();
    http.expectOne(ReleaseUrl).flush({
      tag_name: tag,
      assets: [{ name: 'wydatki.apk', browser_download_url: 'https://github.com/x/wydatki.apk' }],
    });
    await checking;
    fixture.detectChanges();
    return fixture;
  };

  const text = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.textContent?.replace(/\s+/g, ' ') ?? '';

  it('pokazuje nowszą wersję obok zainstalowanej', async () => {
    const fixture = await render('android-v2.2');

    expect(text(fixture)).toContain('Jest nowa wersja aplikacji');
    expect(text(fixture)).toContain('2.2 · masz 2.1');
  });

  it('przy tej samej wersji paska nie ma', async () => {
    const fixture = await render('android-v2.1');

    expect(text(fixture)).not.toContain('Jest nowa wersja');
  });

  it('zamknięty pasek nie wraca dla tej samej wersji, ale wraca dla następnej', async () => {
    const fixture = await render('android-v2.2');
    (fixture.nativeElement.querySelector('.upd__close') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(text(fixture)).not.toContain('Jest nowa wersja');

    expect(text(await render('android-v2.2'))).not.toContain('Jest nowa wersja');
    expect(text(await render('android-v2.3'))).toContain('2.3 · masz 2.1');
  });

  describe('sprawdzanie ręczne i start aplikacji', () => {
    const service = () => TestBed.inject(AppUpdateService);
    const flush = async (tag: string) => {
      await Promise.resolve();
      http.expectOne(ReleaseUrl).flush({ tag_name: tag, assets: [{ name: 'wydatki.apk', browser_download_url: 'https://github.com/x/wydatki.apk' }] });
    };

    it('zimny start pyta GitHuba mimo świeżego sprawdzenia, a powrót z tła nie', async () => {
      vi.spyOn(service() as unknown as { isAndroidApp: () => boolean }, 'isAndroidApp').mockReturnValue(true);
      let onResume: () => void = () => undefined;
      vi.spyOn(service() as unknown as { listenForResume: (cb: () => void) => void }, 'listenForResume')
        .mockImplementation((cb) => { onResume = cb; });
      localStorage.setItem('bt-app-update.checked-at', String(Date.now() - 60_000));

      service().start();
      await flush('android-v2.2');
      await new Promise((r) => setTimeout(r));
      // Blokada godzinna dalej działa przy powrocie z tła: minuta po sprawdzeniu nie pyta drugi raz.
      onResume();
      await Promise.resolve();

      http.expectNone(ReleaseUrl);
      expect(service().status()).toBe('available');
    });

    it('ręczne sprawdzenie pokazuje pasek także po zamknięciu go krzyżykiem dla tej wersji', async () => {
      const fixture = await render('android-v2.2');
      (fixture.nativeElement.querySelector('.upd__close') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(text(fixture)).not.toContain('Jest nowa wersja');

      const checking = service().checkNow();
      await flush('android-v2.2');
      await checking;
      fixture.detectChanges();

      expect(text(fixture)).toContain('2.2 · masz 2.1');
    });

    it('zapisuje wynik: aktualna wersja, nowa wersja i błąd sieci', async () => {
      let checking = service().checkNow();
      expect(service().status()).toBe('checking');
      await flush('android-v2.1');
      await checking;
      expect(service().status()).toBe('upToDate');
      expect(service().installed()).toBe('2.1');
      expect(service().checkedAt()).not.toBeNull();

      checking = service().checkNow();
      await flush('android-v2.3');
      await checking;
      expect([service().status(), service().latest()]).toEqual(['available', '2.3']);

      checking = service().checkNow();
      await Promise.resolve();
      http.expectOne(ReleaseUrl).flush({}, { status: 500, statusText: 'Server Error' });
      await checking;
      expect(service().status()).toBe('error');
    });

    it('wydanie, które nie jest wydaniem Androida, to błąd, a nie „masz najnowszą”', async () => {
      const checking = service().checkNow();
      await flush('v9.9');
      await checking;

      expect(service().status()).toBe('error');
    });
  });
});
