import { Capacitor } from '@capacitor/core';
import { Browser } from '@capacitor/browser';
import { OpenIdConfiguration } from 'angular-auth-oidc-client';

/**
 * Własny schemat adresów aplikacji mobilnej (RFC 8252): pod nim system oddaje przekierowanie z przeglądarki do apki.
 *
 * ⚠️ Musi się zgadzać z `OAuthDefaults.MobileScheme` w BudgetTracker.Identity, z `appId` w `capacitor.config.ts`,
 * z `AndroidManifest.xml` i z `Info.plist` — OpenIddict porównuje `redirect_uri` dosłownie (pilnuje tego
 * `MobileClientTests` po stronie Identity).
 */
export const MOBILE_SCHEME = 'com.wydatki.app';
export const MOBILE_REDIRECT_URL = `${MOBILE_SCHEME}:/auth-callback`;
export const MOBILE_POST_LOGOUT_URL = `${MOBILE_SCHEME}:/logout`;
export const MOBILE_CLIENT_ID = 'budgettracker-mobile';

/** `true` w aplikacji z Capacitora (Android, iOS), `false` w zwykłej przeglądarce. */
export function isNativeApp(): boolean {
  return Capacitor.isNativePlatform();
}

/**
 * Otwiera adres w systemowej przeglądarce (Custom Tabs na Androidzie, SFSafariViewController na iOS), nie w WebView.
 * Logowanie MUSI iść tędy (RFC 8252 §8.12): w WebView aplikacja widziałaby wpisywane hasło, a ciasteczko sesji
 * Identity nie byłoby wspólne z przeglądarką.
 */
export function openInSystemBrowser(url: string): void {
  void Browser.open({ url });
}

/**
 * Konfiguracja OIDC aplikacji mobilnej. Różnice względem SPA:
 * - osobny klient publiczny z powrotem przez własny schemat adresu;
 * - odnawianie REFRESH TOKENEM, nie cichym iframe'em — ciasteczko sesji Identity żyje w systemowej przeglądarce,
 *   a nie w WebView, więc ramka z `prompt=none` zawsze kończyłaby się `login_required`;
 * - `ignoreNonceAfterRefresh`: OpenIddict nie powtarza `nonce` w id_tokenie wydanym przy odświeżeniu.
 */
export function nativeAuthConfig(authority: string): OpenIdConfiguration {
  return {
    authority,
    redirectUrl: MOBILE_REDIRECT_URL,
    postLogoutRedirectUri: MOBILE_POST_LOGOUT_URL,
    clientId: MOBILE_CLIENT_ID,
    scope: 'openid profile email offline_access budgettracker_api',
    responseType: 'code',
    silentRenew: true,
    useRefreshToken: true,
    ignoreNonceAfterRefresh: true,
    secureRoutes: ['/api'],
    autoUserInfo: false,
  };
}
