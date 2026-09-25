import { HttpInterceptorFn } from '@angular/common/http';
import { runtimeConfig } from '../runtime-config';

/**
 * Dokleja adres API do żądań pisanych względnie (`/api/...`).
 *
 * <para>
 * Dzięki temu ani jeden serwis w aplikacji nie musi wiedzieć, gdzie stoi API — lokalnie adres jest pusty
 * i `/api` trafia w proxy `ng serve`, a na Static Web Apps dostaje pełny adres App Service.
 * </para>
 *
 * ⚠️ Musi być OSTATNI w łańcuchu. Wcześniejsze interceptory (`authInterceptor` z `secureRoutes: ['/api']`
 * oraz `languageInterceptor`) rozpoznają żądanie po tym, że adres zaczyna się od `/api/` — przepisanie go
 * na adres bezwzględny wcześniej sprawiłoby, że przestałyby dokładać token i nagłówek języka.
 */
export const apiBaseUrlInterceptor: HttpInterceptorFn = (request, next) => {
  const base = runtimeConfig().apiBaseUrl;

  if (!base || !request.url.startsWith('/api/')) return next(request);

  return next(request.clone({ url: `${base.replace(/\/$/, '')}${request.url}` }));
};
