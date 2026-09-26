import { BootstrapContext, bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { config } from './app/app.config.server';

/**
 * Punkt wejścia prerenderu. ⚠️ Nie jest wdrażany — po zbudowaniu zostaje sam statyczny HTML.
 *
 * ⚠️ `context` MUSI trafić do `bootstrapApplication` jako trzeci argument. Bez tego prerender kończy się
 * `NG0401` („missing platform"), a komunikat nie mówi, czego brakuje — wygląda jak problem z konfiguracją
 * aplikacji, a nie z tym jednym pominiętym parametrem.
 */
export default (context: BootstrapContext) => bootstrapApplication(App, config, context);
