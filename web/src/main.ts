import { bootstrapApplication } from '@angular/platform-browser';
import { loadRuntimeConfig } from './app/core/runtime-config';

/**
 * ⚠️ `app.config` i `App` wchodzą przez import DYNAMICZNY, już po wczytaniu konfiguracji. Import statyczny
 * wykonałby ciało modułu `app.config.ts` natychmiast — a ono czyta adres serwera tożsamości, żeby zbudować
 * `provideAuth`. Przy imporcie statycznym aplikacja wystartowałaby z adresem sprzed wczytania pliku.
 */
loadRuntimeConfig()
  .then(async () => {
    const [{ appConfig }, { App }] = await Promise.all([
      import('./app/app.config'),
      import('./app/app'),
    ]);

    return bootstrapApplication(App, appConfig);
  })
  .catch((err) => console.error(err));
