import { Injectable, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { isNativeApp, openInSystemBrowser } from './native-auth';

/**
 * Jedno miejsce, które wysyła na logowanie i wylogowanie. W przeglądarce to zwykłe przekierowanie biblioteki OIDC,
 * w aplikacji mobilnej adres Identity otwiera się w systemowej przeglądarce, a wynik wraca deep linkiem
 * (patrz `provideNativeAuth`).
 */
@Injectable({ providedIn: 'root' })
export class AuthNavigation {
  private readonly oidc = inject(OidcSecurityService);

  /** Gdzie wrócić po zalogowaniu w apce — przeglądarka pamięta to sama (adres przed przekierowaniem). */
  private returnUrl: string | null = null;

  /** Ustawione od otwarcia logowania do powrotu deep linkiem — w tym czasie apka nie otwiera go drugi raz. */
  signingIn = false;

  signIn(returnUrl?: string): void {
    if (!isNativeApp()) {
      this.oidc.authorize();
      return;
    }

    if (returnUrl) this.returnUrl = returnUrl;
    this.signingIn = true;
    this.oidc.authorize(undefined, { urlHandler: openInSystemBrowser });
  }

  signOut(): void {
    this.oidc.logoff(undefined, isNativeApp() ? { urlHandler: openInSystemBrowser } : undefined).subscribe();
  }

  /** Adres, na który apka przechodzi po udanym powrocie z logowania. Jednorazowy. */
  takeReturnUrl(): string {
    const url = this.returnUrl ?? '/dashboard';
    this.returnUrl = null;
    return url;
  }
}
