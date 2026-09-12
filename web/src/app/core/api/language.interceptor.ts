import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

/**
 * Dokleja do każdego żądania język, w którym AKTUALNIE jest interfejs.
 *
 * ⚠️ Bez tego front i backend mówiły dwoma różnymi językami naraz. Backend ma
 * `UseRequestLocalization` z obsługiwanymi `pl` i `en`, więc bez nagłówka rozstrzygał
 * `Accept-Language` PRZEGLĄDARKI — a front nie pyta przeglądarki o nic, tylko startuje
 * na sztywno z `pl` (patrz `app.config.ts`). Na przeglądarce ustawionej na angielski
 * dawało to polski ekran z angielskim komunikatem błędu z API.
 *
 * Język bierzemy z `TranslateService`, a nie ze stałej, żeby to nie wymagało pamiętania
 * o dwóch miejscach w dniu, w którym pojawi się przełącznik języka.
 */
export const languageInterceptor: HttpInterceptorFn = (request, next) => {
  // ⚠️ TYLKO API, i to sprawdzenie musi być PIERWSZE.
  //
  // Same pliki tłumaczeń (`i18n/*.json`) też idą przez HttpClient — czyli przez ten
  // interceptor. Sięgnięcie po `TranslateService` przy ICH ładowaniu pyta o język usługę,
  // która właśnie ten język wczytuje: żądanie nie dochodziło do skutku i cały interfejs
  // zostawał na surowych kluczach („transactions.errors.loadFailed" zamiast zdania).
  // Nagłówek języka jest zresztą dla statycznego JSON-a bez znaczenia.
  if (!request.url.startsWith('/api/')) return next(request);

  // Nie nadpisujemy nagłówka, który ktoś ustawił świadomie przy konkretnym żądaniu.
  if (request.headers.has('Accept-Language')) return next(request);

  const translate = inject(TranslateService);
  // `currentLang` jest SYGNAŁEM w tej wersji ngx-translate, stąd wywołanie.
  const lang = translate.currentLang() ?? translate.getFallbackLang();
  if (!lang) return next(request);

  return next(request.clone({ setHeaders: { 'Accept-Language': lang } }));
};
