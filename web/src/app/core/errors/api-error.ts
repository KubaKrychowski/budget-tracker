import { HttpErrorResponse } from '@angular/common/http';

/**
 * Komunikat błędu z odpowiedzi API, albo `null`, jeśli odpowiedź żadnego nie niosła.
 *
 * Backend trzyma teksty dla użytkownika w `.resx` i oddaje je w polu `error` ciała odpowiedzi
 * (patrz `DomainExceptionHandler`) — to jest jedyne miejsce, w którym front powinien o tym wiedzieć.
 *
 * ⚠️ Zwraca `null`, a nie tekst zastępczy, CELOWO. Zdanie „nie udało się połączyć" jest
 * tłumaczeniem i należy do warstwy, która ma `TranslateService`; wstawienie go tutaj
 * zaszyłoby polski tekst w kodzie (CLAUDE.md §5).
 */
export function apiErrorText(error: unknown): string | null {
  if (!(error instanceof HttpErrorResponse)) return null;

  // Ciało błędu bywa też stringiem (proxy, 502 z dev-serwera) albo niczym.
  const body = error.error as unknown;
  const message = typeof body === 'object' && body !== null
    ? (body as { error?: unknown }).error
    : body;

  return typeof message === 'string' && message.trim().length > 0 ? message : null;
}

/**
 * Czy to błąd, o którym w ogóle warto powiedzieć użytkownikowi.
 *
 * Przerwane żądanie (status 0) leci przy każdym odświeżeniu i przy wyjściu ze strony —
 * pokazywanie go byłoby szumem, a nie informacją.
 */
export function isReportable(error: unknown): boolean {
  return !(error instanceof HttpErrorResponse && error.status === 0 && error.error instanceof ProgressEvent);
}
