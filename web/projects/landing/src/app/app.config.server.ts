import { ApplicationConfig, mergeApplicationConfig } from '@angular/core';
import { provideServerRendering } from '@angular/platform-server';
import { appConfig } from './app.config';

/**
 * Konfiguracja użyta WYŁĄCZNIE przy generowaniu statycznego HTML-a w trakcie builda.
 *
 * Landing nie ma serwera na produkcji — leci na Static Web Apps jako zbiór plików. Renderowanie po
 * stronie serwera jest tu narzędziem builda, nie architekturą: chodzi o to, żeby `index.html` niósł
 * treść zamiast pustego `<app-root>`, bo boty i podglądy linków nie wykonują JavaScriptu.
 */
export const config: ApplicationConfig = mergeApplicationConfig(appConfig, {
  providers: [provideServerRendering()],
});
