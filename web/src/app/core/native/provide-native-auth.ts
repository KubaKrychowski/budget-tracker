import { Provider, EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { App as CapacitorApp } from '@capacitor/app';
import { Browser } from '@capacitor/browser';
import {
  AbstractSecurityStorage,
  DefaultLocalStorageService,
  OidcSecurityService,
  autoLoginPartialRoutesGuard,
} from 'angular-auth-oidc-client';
import { catchError, firstValueFrom, map, of, take } from 'rxjs';
import { AuthNavigation } from './auth-navigation';
import { MOBILE_POST_LOGOUT_URL, MOBILE_REDIRECT_URL, isNativeApp } from './native-auth';

/**
 * Po tylu milisekundach od powrotu do apki bez sesji i bez deep linku uznajemy, że użytkownik zamknął przeglądarkę
 * z logowaniem. Deep link przychodzi przed `resume` (Android) albo zamiast niego (iOS), więc to tylko zapas.
 */
const ReturnWithoutCallbackMs = 1000;

/**
 * Strażnik całej aplikacji. W przeglądarce to `autoLoginPartialRoutesGuard` biblioteki (przekierowanie na Identity
 * i powrót na `/auth-callback`), w apce mobilnej — logowanie w systemowej przeglądarce i odmowa nawigacji, dopóki
 * deep link nie przyniesie kodu.
 */
export const appAuthGuard: CanActivateFn = (route, state) => {
  if (!isNativeApp()) return autoLoginPartialRoutesGuard(route, state);

  const navigation = inject(AuthNavigation);
  return inject(OidcSecurityService).isAuthenticated$.pipe(
    take(1),
    map(({ isAuthenticated }) => {
      if (isAuthenticated) return true;
      if (!navigation.signingIn) navigation.signIn(state.url);
      return false;
    }),
  );
};

/**
 * Dodatki OIDC tylko dla aplikacji mobilnej. Pusta lista w przeglądarce — tam start sprawdza
 * `withAppInitializerAuthCheck()`.
 *
 * - Magazyn tokenów w localStorage zamiast sessionStorage: system zabija proces apki w tle, a sessionStorage ginie
 *   razem z nim. Bez tego każde przełączenie się na inną aplikację kończyłoby się ponownym logowaniem.
 * - Start przez `checkAuthIncludingServer`: wygasły access token (15 minut) jest odświeżany refresh tokenem,
 *   zanim strażnik uzna, że sesji nie ma.
 * - Deep link `com.wydatki.app:/auth-callback?code=…` wymienia kod na token i zamyka przeglądarkę.
 */
export function provideNativeAuth(): (Provider | EnvironmentProviders)[] {
  if (!isNativeApp()) return [];

  return [
    { provide: AbstractSecurityStorage, useClass: DefaultLocalStorageService },
    provideAppInitializer(() => {
      const oidc = inject(OidcSecurityService);
      const router = inject(Router);
      const navigation = inject(AuthNavigation);

      /** Wymiana kodu na token w toku — wolna sieć nie może być wzięta za porzucone logowanie. */
      let exchanging = false;

      void CapacitorApp.addListener('appUrlOpen', ({ url }) => {
        if (url.startsWith(MOBILE_REDIRECT_URL)) {
          void Browser.close().catch(() => undefined);
          exchanging = true;
          oidc.checkAuth(url).subscribe({
            next: ({ isAuthenticated }) => {
              exchanging = false;
              navigation.signingIn = false;
              if (isAuthenticated) void router.navigateByUrl(navigation.takeReturnUrl());
            },
            error: () => {
              exchanging = false;
              navigation.signingIn = false;
            },
          });
          return;
        }

        if (url.startsWith(MOBILE_POST_LOGOUT_URL)) {
          void Browser.close().catch(() => undefined);
          void router.navigateByUrl('/');
        }
      });

      // Zamknięta przeglądarka bez zalogowania zostawiłaby samą okładkę — otwieramy logowanie jeszcze raz.
      const retrySignIn = () => setTimeout(() => {
        if (exchanging || oidc.authenticated().isAuthenticated) return;
        navigation.signingIn = false;
        void router.navigateByUrl('/');
      }, ReturnWithoutCallbackMs);
      void CapacitorApp.addListener('resume', retrySignIn);
      void Browser.addListener('browserFinished', retrySignIn);

      // Systemowy „wstecz" na Androidzie: historia aplikacji, a na jej początku wyjście — nie zamknięcie bez ostrzeżenia
      // z dowolnego ekranu.
      void CapacitorApp.addListener('backButton', ({ canGoBack }) => {
        if (canGoBack) window.history.back();
        else void CapacitorApp.exitApp();
      });

      return firstValueFrom(oidc.checkAuthIncludingServer().pipe(catchError(() => of(null))));
    }),
  ];
}
