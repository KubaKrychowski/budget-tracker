import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';

/**
 * Konfiguracja landingu — celowo uboga w porównaniu z aplikacją.
 *
 * Nie ma tu routera (strona jest jedna; dokumenty to statyczny HTML w `public/`), HttpClienta (formularz używa
 * `fetch`), ngx-translate (jeden język, patrz doc `App`), animacji ani ikon NG-ZORRO (strona nie używa jego
 * komponentów — przyciski i pola to zwykły HTML ostylowany tokenami). Każda z tych rzeczy weszłaby do bundla.
 */
export const appConfig: ApplicationConfig = {
  providers: [provideBrowserGlobalErrorListeners()],
};
