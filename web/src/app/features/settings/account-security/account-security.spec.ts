import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { provideTranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { IDENTITY_AUTHORITY } from '../../../app.config';
import { AccountSecurity } from './account-security';

/**
 * Zakładka „Konto i bezpieczeństwo": pilnujemy dwóch reguł, których nie widać na oko.
 * Sekcja „Administracja" ma się pokazać WYŁĄCZNIE adminowi (rola jedzie w id_tokenie jako string albo tablica),
 * a „Usuń konto" ma być zawsze i prowadzić do Identity, nie do żadnego wywołania z Angulara.
 */
describe('AccountSecurity', () => {
  function create(claims: Record<string, unknown>): ComponentFixture<AccountSecurity> {
    TestBed.configureTestingModule({
      imports: [AccountSecurity],
      providers: [
        provideNoopAnimations(),
        provideTranslateService({ lang: 'pl', fallbackLang: 'pl' }),
        {
          provide: OidcSecurityService,
          useValue: {
            userData$: of({ userData: claims, allUserData: [] }),
            // Pusty idToken = brak świeżych roszczeń, komponent zostaje przy userData$.
            forceRefreshSession: () => of({ idToken: '' }),
            logoff: () => of(null),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(AccountSecurity);
    fixture.detectChanges();
    return fixture;
  }

  const links = (fixture: ComponentFixture<AccountSecurity>): string[] =>
    Array.from<HTMLAnchorElement>(fixture.nativeElement.querySelectorAll('a[href]')).map((a) => a.getAttribute('href') ?? '');

  it('hides the administration section from a regular user', () => {
    const fixture = create({ email: 'jan@example.com', role: [] });

    expect(links(fixture).some((href) => href.includes('/Admin/'))).toBe(false);
  });

  it('shows the administration section when the role claim is the string "admin"', () => {
    const fixture = create({ email: 'jan@example.com', role: 'admin' });

    expect(links(fixture)).toContain(`${IDENTITY_AUTHORITY}/Admin/Users`);
  });

  it('shows the administration section when "admin" is one of several roles', () => {
    const fixture = create({ email: 'jan@example.com', role: ['user', 'admin'] });

    expect(links(fixture)).toContain(`${IDENTITY_AUTHORITY}/Admin/Users`);
  });

  it('does not treat a role that merely contains "admin" as an administrator', () => {
    const fixture = create({ email: 'jan@example.com', role: 'administrator' });

    expect(links(fixture).some((href) => href.includes('/Admin/'))).toBe(false);
  });

  it('always offers account deletion, and it leads to the identity server', () => {
    const fixture = create({ email: 'jan@example.com' });

    expect(links(fixture).some((href) => href.startsWith(`${IDENTITY_AUTHORITY}/Account/DeleteAccount?returnUrl=`))).toBe(true);
  });

  const tags = (fixture: ComponentFixture<AccountSecurity>): string[] =>
    Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('nz-tag')).map((t) => t.textContent?.trim() ?? '');

  it('mowi, ze 2FA jest WYLACZONA, gdy jest wylaczona', () => {
    // Zielony znacznik "wlaczona" stal w szablonie bezwarunkowo, wiec ekran twierdzil
    // jednoczesnie, ze 2FA jest i wlaczona, i wylaczona. Przy pytaniu o bezpieczenstwo konta
    // to najgorszy mozliwy rodzaj bledu: uzytkownik nie wie, ktoremu zdaniu wierzyc.
    const fixture = create({ email: 'jan@example.com', role: [], two_factor_enabled: 'false' });

    expect(tags(fixture)).toEqual(['settings.account.twoFactor.disabled']);
  });

  it('mowi, ze 2FA jest wlaczona, gdy jest wlaczona', () => {
    const fixture = create({ email: 'jan@example.com', role: [], two_factor_enabled: 'true' });

    expect(tags(fixture)).toEqual(['settings.account.twoFactor.enabled']);
  });
});
