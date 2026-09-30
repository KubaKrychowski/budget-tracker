import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { SessionRecovery } from './session-recovery';

/**
 * Odpowiedź 401 z API oznacza wygasłą albo brakującą sesję — zamiast pokazywać błąd na każdym ekranie, próbuje ją
 * odnowić, a gdy się nie da, odsyła na logowanie.
 *
 * @remarks
 * - ⚠️ Musi stać PRZED `authInterceptor()` z biblioteki OIDC: ponowione żądanie ma przejść przez niego jeszcze raz, żeby
 *   dostać ŚWIEŻY token. Interceptor stojący za nim ponawiałby żądanie z tym samym, już odrzuconym nagłówkiem.
 * - Problem, który to zamyka: po długiej przerwie (uśpiony komputer, karta odtworzona z poprzedniej sesji) w storage
 *   siedział wygasły token, a odnowienie w tle nie miało czym się udać. Biblioteka nie dawała wtedy żadnego sygnału
 *   aplikacji, więc każdy ekran kończył się 401 zamiast odświeżeniem albo wylogowaniem.
 * - Tylko `/api/` — pliki tłumaczeń i inne zasoby statyczne nie mają nic wspólnego z sesją.
 * - Ponawiamy RAZ: drugi 401 na ponowionym żądaniu to już nie kwestia tokenu, tylko sesji, której nie da się uratować,
 *   więc idzie na logowanie zamiast zapętlać odświeżanie.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) return next(request);

  const recovery = inject(SessionRecovery);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (!isUnauthorized(error)) return throwError(() => error);

      return recovery.refresh().pipe(
        switchMap((renewed) => {
          if (!renewed) {
            recovery.signIn();
            return throwError(() => error);
          }

          return next(request).pipe(
            catchError((retryError: unknown) => {
              if (isUnauthorized(retryError)) recovery.signIn();
              return throwError(() => retryError);
            }),
          );
        }),
      );
    }),
  );
};

const isUnauthorized = (error: unknown): boolean => error instanceof HttpErrorResponse && error.status === 401;
