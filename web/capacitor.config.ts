import type { CapacitorConfig } from '@capacitor/cli';

/**
 * Aplikacja mobilna Wydatki.com — ten sam front Angulara w WebView (Android, iOS), budowany konfiguracją `mobile`
 * (`npm run build:mobile`), która podmienia `config.json` na adresy produkcji z `mobile/config.json`.
 *
 * ⚠️ `appId` to zarazem schemat adresu powrotu z logowania (`com.wydatki.app:/auth-callback`) — patrz
 * `src/app/core/native/native-auth.ts` i `OAuthDefaults.MobileScheme` w BudgetTracker.Identity.
 *
 * ⚠️ Originy WebView (`https://localhost` na Androidzie, `capacitor://localhost` na iOS) są zarejestrowane w CORS API
 * i Identity. Zmiana `server.hostname` albo `androidScheme` wymaga zmiany tam też.
 */
const config: CapacitorConfig = {
  appId: 'com.wydatki.app',
  appName: 'Wydatki.com',
  webDir: 'dist/mobile/browser',
  plugins: {
    // Tryb pełnoekranowy obsługuje MainActivity.java, a treść celowo ignoruje otwór na kamerę. Wtyczka SystemBars
    // dokładała margines nad WebView (na WebView < 140), czyli pas w miejscu ukrytego paska statusu.
    SystemBars: {
      insetsHandling: 'disable',
    },
  },
};

export default config;
