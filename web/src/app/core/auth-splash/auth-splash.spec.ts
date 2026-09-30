import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { AuthSplash } from './auth-splash';
import { AuthSplashScreen } from './auth-splash-screen';
import { PendingRequests } from './pending-requests';

@Component({ template: '' })
class BlankPage {}

/**
 * Okładka logowania.
 *
 * Reguły, których nie widać z ekranu: pojawia się tylko przy autentykacji (nie przy odświeżeniu z ważną sesją),
 * nie znika, dopóki ekran czeka na dane, i nigdy nie odkrywa aplikacji bez sesji.
 */
describe('AuthSplash', () => {
  let authenticated: ReturnType<typeof signal<{ isAuthenticated: boolean }>>;

  const create = (startPath = '/'): AuthSplash => {
    window.history.replaceState({}, '', startPath);
    return TestBed.inject(AuthSplash);
  };

  const navigate = async (): Promise<void> => {
    await TestBed.inject(Router).navigateByUrl('/dashboard');
    TestBed.tick();
  };

  beforeEach(() => {
    vi.useFakeTimers();
    authenticated = signal({ isAuthenticated: true });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', component: BlankPage }]),
        provideTranslateService(),
        { provide: OidcSecurityService, useValue: { authenticated } },
      ],
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    window.history.replaceState({}, '', '/');
  });

  it('odświeżenie strony z ważną sesją nie pokazuje okładki', async () => {
    const splash = create('/dashboard');
    TestBed.tick();
    await navigate();
    await vi.advanceTimersByTimeAsync(1000);

    expect(splash.visible()).toBe(false);
  });

  it('brak sesji zapala okładkę z napisem o logowaniu, jeszcze zanim ruszy przekierowanie', () => {
    authenticated.set({ isAuthenticated: false });
    const splash = create('/dashboard');
    TestBed.tick();

    expect(splash.visible()).toBe(true);
    expect(splash.status()).toBe('signingIn');
  });

  it('powrót z logowania zaczyna od okładki i trzyma ją, dopóki ekran czeka na dane', async () => {
    const splash = create('/auth-callback?code=abc');
    const pending = TestBed.inject(PendingRequests);
    pending.increment();
    TestBed.tick();
    await navigate();

    expect(splash.visible()).toBe(true);
    expect(splash.status()).toBe('loading');

    // Dane wciąż lecą — nawet długo po końcu nawigacji okładka zostaje.
    await vi.advanceTimersByTimeAsync(AuthSplash.SettleMs * 4);
    expect(splash.visible()).toBe(true);

    pending.decrement();
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(AuthSplash.SettleMs);
    expect(splash.visible()).toBe(false);
  });

  it('nowe żądanie w okresie ciszy odsuwa zniknięcie okładki', async () => {
    // Ekran zaczyna żądania po własnym pierwszym renderze — sam koniec nawigacji byłby za wcześnie.
    const splash = create('/auth-callback?code=abc');
    TestBed.tick();
    await navigate();
    await vi.advanceTimersByTimeAsync(AuthSplash.SettleMs - 50);

    const pending = TestBed.inject(PendingRequests);
    pending.increment();
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(AuthSplash.SettleMs * 2);
    expect(splash.visible()).toBe(true);

    pending.decrement();
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(AuthSplash.SettleMs);
    expect(splash.visible()).toBe(false);
  });

  it('bezpiecznik odkrywa aplikację mimo trwających żądań, ale tylko z sesją', async () => {
    const splash = create('/auth-callback?code=abc');
    TestBed.inject(PendingRequests).increment();
    TestBed.tick();

    await vi.advanceTimersByTimeAsync(AuthSplash.MaxMs);
    expect(splash.visible()).toBe(false);
  });

  it('bezpiecznik nie odkrywa aplikacji bez sesji', async () => {
    authenticated.set({ isAuthenticated: false });
    const splash = create('/auth-callback?code=abc');
    TestBed.tick();

    await vi.advanceTimersByTimeAsync(AuthSplash.MaxMs * 2);
    expect(splash.visible()).toBe(true);
  });
});

describe('AuthSplashScreen', () => {
  it('pokazuje przetłumaczony status zamiast klucza i nie ma go przed załadowaniem tłumaczeń', async () => {
    TestBed.configureTestingModule({ imports: [AuthSplashScreen], providers: [provideTranslateService()] });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      app: { title: 'Wydatki.com', version: 'v.2.0' },
      auth: { splash: { signingIn: 'Logowanie…', loading: 'Wczytuję Twoje dane…' } },
    });
    translate.use('pl');

    const fixture = TestBed.createComponent(AuthSplashScreen);
    fixture.componentRef.setInput('status', 'loading');
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Wczytuję Twoje dane…');
    expect(text).toContain('Wydatki.com');
    expect(text).not.toContain('auth.splash');

    fixture.componentRef.setInput('status', 'signingIn');
    await fixture.whenStable();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Logowanie…');
  });
});
