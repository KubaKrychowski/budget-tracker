import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TranslatePipe } from '@ngx-translate/core';
import { IDENTITY_AUTHORITY } from '../../../app.config';

/**
 * Dekoduje payload JWT (bez weryfikacji podpisu — to front, weryfikację robi biblioteka OIDC
 * przy właściwym logowaniu). Potrzebne, bo `forceRefreshSession()` w `userData$`/`LoginResponse.userData`
 * potrafi zwrócić STARY, buforowany obiekt mimo świeżo wydanego `idToken` (sprawdzone empirycznie
 * z `autoUserInfo: false`) — jedyne wiarygodne źródło świeżych roszczeń to ręczne odczytanie tokenu.
 */
function decodeJwtPayload(token: string): Record<string, unknown> {
  const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const json = decodeURIComponent(
    atob(base64).split('').map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0')).join(''),
  );
  return JSON.parse(json);
}

/**
 * Zakładka „Konto i bezpieczeństwo" — tylko podgląd i przekierowania. Sama logika (zmiana
 * hasła, włączanie/wyłączanie 2FA) zostaje w BudgetTracker.Identity (Razor): apka Angular
 * świadomie nie ma własnego UI logowania/haseł. Status 2FA i e-mail jadą w roszczeniach
 * id_tokenu — front nie ma bezpośredniego dostępu do bazy Identity.
 */
@Component({
  selector: 'app-account-security',
  imports: [NzButtonModule, NzTagModule, TranslatePipe],
  templateUrl: './account-security.html',
  styleUrl: './account-security.scss',
})
export class AccountSecurity {
  private readonly oidcSecurityService = inject(OidcSecurityService);

  private readonly userData = toSignal(this.oidcSecurityService.userData$, { initialValue: null });

  /** Roszczenia ze świeżo wymuszonego odświeżenia (patrz `decodeJwtPayload`) — nadpisują cache. */
  private readonly refreshedClaims = signal<Record<string, unknown> | null>(null);

  constructor() {
    // Status 2FA i e-mail jadą w id_tokenie, który front trzyma w sessionStorage aż do
    // wygaśnięcia — po powrocie z Identity (włączenie/wyłączenie 2FA, nowe kody) stary token
    // wciąż pokazywałby POPRZEDNI stan. Silent renew od razu po wejściu na tę zakładkę
    // (ukryty iframe, to samo `silentRenewUrl` co reszta aplikacji) pobiera świeży token.
    this.oidcSecurityService.forceRefreshSession().subscribe((response) => {
      if (response.idToken) this.refreshedClaims.set(decodeJwtPayload(response.idToken));
    });
  }

  private readonly claims = computed(() => this.refreshedClaims() ?? this.userData()?.userData ?? null);
  protected readonly email = computed(() => this.claims()?.['email'] as string | undefined);

  // Roszczenie jedzie jako dosłowny string "true"/"false" (patrz AuthorizationController
  // w BudgetTracker.Identity) — JWT nie gwarantuje typu JSON boolean dla własnych roszczeń.
  protected readonly twoFactorEnabled = computed(() => this.claims()?.['two_factor_enabled'] === 'true');

  // Akcje 2FA/hasła to pełne przejścia na Identity (cookie sesji, nie bearer) — po zakończeniu
  // Identity odsyła z powrotem tutaj, więc returnUrl to bieżący adres tej zakładki.
  private readonly returnUrl = encodeURIComponent(window.location.href);

  protected readonly enableTwoFactorUrl =
    `${IDENTITY_AUTHORITY}/Account/EnableAuthenticator?returnUrl=${this.returnUrl}`;
  protected readonly disableTwoFactorUrl =
    `${IDENTITY_AUTHORITY}/Account/DisableTwoFactor?returnUrl=${this.returnUrl}`;
  protected readonly regenerateRecoveryCodesUrl =
    `${IDENTITY_AUTHORITY}/Account/RegenerateRecoveryCodes?returnUrl=${this.returnUrl}`;

  /** Rola `admin` jedzie w id_tokenie jako string albo tablica (jedna rola vs. kilka) — patrz AuthorizationController w Identity. */
  protected readonly isAdmin = computed(() => {
    const role = this.claims()?.['role'];
    return Array.isArray(role) ? role.includes('admin') : role === 'admin';
  });

  // Ekrany administracyjne i usuwanie konta mieszkają w Identity (Razor) i chroni je ciasteczko z rolą albo hasło,
  // nie token z Angulara — link to zwykłe przejście, jak przy 2FA.
  protected readonly adminUsersUrl = `${IDENTITY_AUTHORITY}/Admin/Users`;
  protected readonly deleteAccountUrl =
    `${IDENTITY_AUTHORITY}/Account/DeleteAccount?returnUrl=${this.returnUrl}`;

  protected readonly changePasswordUrl = computed(() => {
    const email = this.email();
    return `${IDENTITY_AUTHORITY}/Account/ForgotPassword${email ? `?email=${encodeURIComponent(email)}` : ''}`;
  });

  /** Ten sam mechanizm co przycisk wylogowania w nagłówku (`App.logout`) — RP-initiated logout. */
  protected logout(): void {
    this.oidcSecurityService.logoff().subscribe();
  }
}
