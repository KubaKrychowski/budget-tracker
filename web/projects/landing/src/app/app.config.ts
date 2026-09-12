import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { LANDING_ICONS } from './icons';

/**
 * Konfiguracja landingu — celowo uboga w porównaniu z aplikacją.
 *
 * Nie ma tu routera (strona jest jedna), HttpClienta (nic nie woła API), ngx-translate
 * (jeden język, patrz doc <see cref="App" />) ani NG-ZORRO i18n (nie ma komponentów
 * z własnymi tekstami). Każda z tych rzeczy weszłaby do bundla, a landing i tak płaci
 * już za runtime Angulara — patrz rewizja w `plans/landing-page.md`.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // `nz-alert` z zamykaniem i `nz-button` w stanie ładowania animują się przez Angular
    // Animations. Bez tego providera komponenty overlayowe potrafią zostać w stanie pośrednim.
    provideAnimationsAsync(),
    provideNzIcons(LANDING_ICONS),
  ],
};
