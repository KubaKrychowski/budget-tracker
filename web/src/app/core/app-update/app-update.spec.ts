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
});
