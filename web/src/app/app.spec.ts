import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { of } from 'rxjs';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { APP_ICONS } from './core/icons';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        // App renderuje <router-outlet />, więc potrzebuje routera w teście.
        provideRouter([]),
        provideNzIcons(APP_ICONS),
        // Bez loadera — test nie sięga po pliki i18n, a TranslatePipe zwraca klucze.
        // Sprawdzamy strukturę shella, nie treść tłumaczeń.
        provideTranslateService(),
        // Zaślepka zamiast pełnego provideAuth() — test sprawdza strukturę shella,
        // nie prawdziwe logowanie, więc nie ma po co ciągnąć całej konfiguracji OIDC.
        { provide: OidcSecurityService, useValue: { logoff: () => of(undefined) } },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render the router outlet shell', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });

  it('ikona podręcznika w nagłówku prowadzi do /handbook, obok zębatki ustawień (#19)', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    // Terminal (#25) i wylogowanie doszły do tej samej grupy ikon PO tym teście (#19) —
    // liczymy pozycje względem siebie, nie sztywną liczbę, żeby kolejny dopisany przycisk
    // nie wymagał poprawki tutaj za każdym razem.
    const links = Array.from(compiled.querySelectorAll('.app-header__icon-link'));
    const handbookIndex = links.findIndex((el) => el.getAttribute('href')?.includes('/handbook'));
    const settingsIndex = links.findIndex((el) => el.getAttribute('href') === '/settings');
    expect(handbookIndex).toBeGreaterThanOrEqual(0);
    expect(settingsIndex).toBeGreaterThan(handbookIndex);
  });
});
