import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { filter, map } from 'rxjs';
import { PendingRequests } from './pending-requests';

/** Co splash mówi: `signingIn` — jeszcze nie ma sesji, `loading` — sesja jest, ekran czeka na dane. */
export type AuthSplashStatus = 'signingIn' | 'loading';

/**
 * Pełnoekranowa okładka na czas logowania — zamiast powłoki z samą górną belką, przekierowania i ładowania.
 *
 * @remarks
 * ⚠️ Tylko przy AUTENTYKACJI, nie przy każdym wejściu na stronę. Startuje widoczna wtedy, gdy przeglądarka wraca
 * z logowania (`/auth-callback`), a zapala się też, gdy sesji nie ma (przekierowanie na Identity trwa). Odświeżenie
 * strony z ważną sesją splasha nie pokazuje.
 *
 * Znika, gdy jednocześnie: jest sesja, pierwsza nawigacja się zakończyła i żadne żądanie nie leci przez
 * {@link AuthSplash.SettleMs} — ekran zaczyna żądania po własnym pierwszym renderze, więc sam koniec nawigacji byłby
 * za wcześnie. Bezpiecznik {@link AuthSplash.MaxMs} odkrywa aplikację mimo trwających żądań, żeby wolne API nie
 * zamroziło ekranu; nigdy nie odkrywa jej bez sesji.
 */
@Injectable({ providedIn: 'root' })
export class AuthSplash {
  /** Cisza bez żądań, po której uznajemy, że ekran jest gotowy. */
  static readonly SettleMs = 250;

  /** Najdłużej trzymamy okładkę, gdy sesja już jest. */
  static readonly MaxMs = 10_000;

  private readonly oidc = inject(OidcSecurityService);
  private readonly router = inject(Router);
  private readonly pending = inject(PendingRequests);

  private readonly authenticated = computed(() => this.oidc.authenticated().isAuthenticated);

  private readonly navigated = toSignal(
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd), map(() => true)),
    { initialValue: false },
  );

  /** Powrót z Identity zaczyna od okładki — kod z adresu jest wymieniany na token, zanim cokolwiek się wyrenderuje. */
  readonly visible = signal(window.location.pathname.startsWith('/auth-callback'));

  readonly status = computed<AuthSplashStatus>(() => (this.authenticated() ? 'loading' : 'signingIn'));

  /** Brak sesji = zaraz przekierowanie na logowanie (albo wylogowanie): powłoka nie ma prawa błysnąć. */
  private readonly showWhenSignedOut = effect(() => {
    if (!this.authenticated()) this.visible.set(true);
  });

  private readonly hideWhenReady = effect((onCleanup) => {
    if (!this.visible() || !this.authenticated() || !this.navigated() || this.pending.count() > 0) return;
    const timer = setTimeout(() => this.visible.set(false), AuthSplash.SettleMs);
    onCleanup(() => clearTimeout(timer));
  });

  constructor() {
    if (!this.visible()) return;
    setTimeout(() => {
      if (this.authenticated()) this.visible.set(false);
    }, AuthSplash.MaxMs);
  }
}
