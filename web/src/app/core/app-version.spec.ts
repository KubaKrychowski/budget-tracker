import { TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { vi } from 'vitest';
import { AppVersion } from './app-version';
import { AuthSplashScreen } from './auth-splash/auth-splash-screen';

/**
 * Napis z wersją pod nazwą aplikacji. Wcześniej „v.2.1” siedziało na sztywno w tłumaczeniach, więc telefon z 2.4
 * dalej pokazywał 2.1 na okładce logowania.
 */
describe('AppVersion', () => {
  const stub = (native: boolean, installed = '2.4') => {
    vi.spyOn(AppVersion.prototype as unknown as { isNativeApp: () => boolean }, 'isNativeApp').mockReturnValue(native);
    vi.spyOn(AppVersion.prototype as unknown as { readInstalled: () => Promise<string> }, 'readInstalled').mockResolvedValue(installed);
  };

  const splashText = async (): Promise<string> => {
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', { app: { title: 'Wydatki.com', version: 'v.2.1' }, auth: { splash: { loading: 'Wczytuję dane' } } });
    translate.use('pl');
    const fixture = TestBed.createComponent(AuthSplashScreen);
    fixture.componentRef.setInput('status', 'loading');
    fixture.detectChanges();
    await new Promise((r) => setTimeout(r));
    fixture.detectChanges();
    return (fixture.nativeElement.querySelector('.splash__version') as HTMLElement).textContent?.trim() ?? '';
  };

  beforeEach(() => TestBed.configureTestingModule({ providers: [provideTranslateService()] }));
  afterEach(() => vi.restoreAllMocks());

  it('w aplikacji natywnej bierze numer wydania z aplikacji, a nie z tłumaczeń', async () => {
    stub(true, '2.4');
    const version = TestBed.inject(AppVersion);
    await new Promise((r) => setTimeout(r));

    expect(version.native()).toBe('2.4');
    expect(version.label('2.4')).toBe('v.2.4');
    expect(await splashText()).toBe('v.2.4');
  });

  it('w przeglądarce zostaje napis z tłumaczeń (numeru wydania tam nie ma)', async () => {
    stub(false);

    expect(TestBed.inject(AppVersion).native()).toBeNull();
    expect(await splashText()).toBe('v.2.1');
  });

  it('gdy wtyczka nie odpowie, okładka dalej pokazuje napis z tłumaczeń zamiast pustego miejsca', async () => {
    vi.spyOn(AppVersion.prototype as unknown as { isNativeApp: () => boolean }, 'isNativeApp').mockReturnValue(true);
    vi.spyOn(AppVersion.prototype as unknown as { readInstalled: () => Promise<string> }, 'readInstalled').mockRejectedValue(new Error('unavailable'));

    expect(await splashText()).toBe('v.2.1');
  });
});
