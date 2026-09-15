import { ApplicationConfig, ErrorHandler, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { routes } from './app.routes';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { registerLocaleData } from '@angular/common';
import pl from '@angular/common/locales/pl';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { pl as plDateFns } from 'date-fns/locale';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { APP_ICONS } from './core/icons';
import { NzModalService } from 'ng-zorro-antd/modal';
import { GlobalErrorHandler } from './core/errors/global-error-handler';
import { languageInterceptor } from './core/api/language.interceptor';
import { provideMarkdown } from 'ngx-markdown';

registerLocaleData(pl);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Interceptor jezyka: front startuje na sztywno z `pl`, a backend bez nagłówka slucha
    // przegladarki — bez tego polski ekran potrafil pokazac angielski komunikat z API.
    provideHttpClient(withFetch(), withInterceptors([languageInterceptor])),
    // NG-ZORRO animuje dropdowny, tooltipy i datepickery — bez tego komponenty
    // overlayowe potrafią zostać w stanie pośrednim.
    provideAnimationsAsync(),

    // Teksty UI żyją w `public/i18n/*.json`, nie w szablonach.
    // `pl` jest zarówno językiem startowym, jak i awaryjnym — angielski istnieje,
    // żeby struktura była gotowa na kolejny język, nie dlatego, że go teraz używamy.
    provideTranslateService({
      lang: 'pl',
      fallbackLang: 'pl',
      loader: provideTranslateHttpLoader({ prefix: 'i18n/', suffix: '.json' }),
    }),

    // To osobna warstwa od ngx-translate: NG-ZORRO tłumaczy własne teksty
    // (nazwy miesięcy w datepickerze, „brak danych" w tabelach) swoim mechanizmem.
    provideNzI18n(pl_PL),
    // ⚠️ Nazwy miesięcy i dni w kalendarzu formatuje adapter date-fns, NIE `pl_PL` powyżej — bez jawnego
    // języka datepicker mówił „Aug / Mo Tu We” na polskim ekranie.
    provideNzDateFnsAdapter({ locale: plDateFns, firstDayOfWeek: 1 }),
    provideNzIcons(APP_ICONS),

    // Podręcznik (`features/handbook`) ładuje treść z `public/handbook/*.md` przez
    // `<markdown [src]>` — `provideMarkdown()` rejestruje `MarkdownService` używany przez
    // ten komponent; sam parser (marked) jest wymaganym peer dependency ngx-markdown.
    provideMarkdown(),

    // `@Injectable()` bez `providedIn: 'root'` (tak jest opakowany w NG-ZORRO) — bez
    // jawnego providera tutaj `ConfirmDialogService` (który go wstrzykuje i SAM jest
    // `providedIn: 'root'`) nie znalazłby go w drzewie wstrzykiwań przy pierwszym użyciu.
    NzModalService,

    // Ostatnia siatka pod błędami, których nie obsłużył żaden ekran. Domyślny handler
    // Angulara pisze do konsoli i na tym kończy — a użytkownik przed ekranem konsoli
    // nie ogląda i nie ma jak odróżnić awarii od tego, że coś się jeszcze ładuje.
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
  ],
};
