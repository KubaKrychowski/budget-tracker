import { Injectable, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable, catchError, finalize, map, of, shareReplay } from 'rxjs';
import { AuthNavigation } from '../native/auth-navigation';

/**
 * Wspólny punkt odzyskiwania sesji po odpowiedzi 401 z API — odświeżenie tokenu albo powrót na logowanie.
 *
 * @remarks
 * Stan (trwające odświeżanie, trwające przekierowanie) żyje w serwisie, a nie w interceptorze, bo równolegle
 * leci wiele żądań i każde z nich może dostać 401 naraz: bez wspólnego stanu każde odpalałoby własne odświeżenie
 * w ukrytej ramce, a własne przekierowanie na logowanie.
 */
@Injectable({ providedIn: 'root' })
export class SessionRecovery {
  private readonly oidc = inject(OidcSecurityService);
  private readonly navigation = inject(AuthNavigation);

  private inFlight: Observable<boolean> | null = null;
  private signingIn = false;

  /**
   * Próbuje odnowić sesję cichym odświeżeniem; `true`, gdy się udało. Równoległe wywołania dzielą jedno odświeżenie.
   * Błąd odświeżenia (timeout ramki, brak sesji na serwerze tożsamości) to zwykłe `false`, nie wyjątek.
   */
  refresh(): Observable<boolean> {
    this.inFlight ??= this.oidc.forceRefreshSession().pipe(
      map((result) => result.isAuthenticated),
      catchError(() => of(false)),
      finalize(() => { this.inFlight = null; }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.inFlight;
  }

  /**
   * Czyści lokalną sesję i przekierowuje na logowanie. Idempotentne: kolejne wołania w tej samej stronie nic nie robią,
   * bo przekierowanie już trwa.
   *
   * ⚠️ `logoffLocal` PRZED `authorize`: sam `authorize` zostawiłby w storage wygasły token, który interceptor
   * dalej doklejałby do żądań lecących jeszcze przed opuszczeniem strony.
   */
  signIn(): void {
    if (this.signingIn) return;
    this.signingIn = true;
    this.oidc.logoffLocal();
    this.navigation.signIn();
  }
}
