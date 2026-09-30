import { HttpInterceptorFn } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';

/**
 * Ile żądań HTTP jest w tej chwili w locie — z tego splash po zalogowaniu wie, że pierwszy ekran skończył się ładować.
 *
 * @remarks
 * Liczy WSZYSTKIE żądania przez `HttpClient` (także pliki tłumaczeń), bo dla splasha liczy się jedno: czy ekran
 * jeszcze czeka na dane. Zwykły licznik, bez rozróżniania adresów — wyjątki byłyby kolejną listą do pamiętania.
 */
@Injectable({ providedIn: 'root' })
export class PendingRequests {
  private readonly inFlight = signal(0);

  readonly count = this.inFlight.asReadonly();

  increment(): void {
    this.inFlight.update((n) => n + 1);
  }

  decrement(): void {
    this.inFlight.update((n) => Math.max(0, n - 1));
  }
}

/**
 * ⚠️ Pierwszy na liście interceptorów: żądanie ponawiane po odświeżeniu tokenu (`unauthorizedInterceptor`) ma się
 * liczyć jako jedno, od pierwszej próby do ostatniej odpowiedzi — inaczej licznik na chwilę spadłby do zera
 * i splash zniknąłby, gdy ekran wciąż czeka na dane.
 */
export const pendingRequestsInterceptor: HttpInterceptorFn = (request, next) => {
  const pending = inject(PendingRequests);
  pending.increment();
  return next(request).pipe(finalize(() => pending.decrement()));
};
