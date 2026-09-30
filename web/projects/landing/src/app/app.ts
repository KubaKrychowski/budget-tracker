import { DOCUMENT } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';

/**
 * Landing — prezentacja PROJEKTU, nie landing produktu.
 *
 * <para>
 * Rozstrzygnięcie z issue #12: adresatem jest ktoś oceniający warsztat, nie ktoś szukający
 * narzędzia do budżetu. Powód jest twardy, nie estetyczny — aplikacja jest single-user,
 * bez rejestracji i bez hostingu, więc przycisk „Wypróbuj za darmo” nie ma dokąd prowadzić.
 * Strona, która obiecuje coś, czego nie ma czym spełnić, szkodzi bardziej niż jej brak.
 * </para>
 *
 * <para>
 * ⚠️ Cała treść jest tu na sztywno po polsku, wbrew konwencji aplikacji (teksty w
 * `public/i18n/*.json`). To świadome odstępstwo: landing jest jednojęzyczny z założenia
 * (wersja angielska jest jawnie poza zakresem #12), a przepuszczenie prozy marketingowej
 * przez klucze tłumaczeń zamieniłoby tekst możliwy do przeczytania i ocenienia w recenzji
 * na listę identyfikatorów. Gdy dojdzie druga wersja językowa, to się zmienia.
 * </para>
 *
 * <para>
 * Sekcje NIE są osobnymi komponentami. Landing to dokument liniowy bez stanu i bez powtórzeń —
 * siedem komponentów po jednym użyciu byłoby rusztowaniem na zapas, którego ten projekt unika
 * (CLAUDE.md §10). Jeśli któraś sekcja zacznie żyć własnym życiem, wtedy się ją wyciągnie.
 * </para>
 */
@Component({
  selector: 'app-root',
  imports: [NzAlertModule, NzButtonModule, NzDividerModule, NzIconModule, NzTagModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  /**
   * Jedyne prawdziwe CTA na tej stronie — patrz doc klasy.
   *
   * ⚠️ Musi wskazywać repozytorium **publiczne**. Do 2026-09-12 stał tu adres repo prywatnego,
   * czyli jedyny przycisk na stronie dawał każdemu odwiedzającemu 404 — a to psuje dokładnie
   * to, po co ta strona istnieje. Kod przeniósł się do `budget-tracker`, bo starego repo nie
   * dało się bezpiecznie upublicznić (historia i `refs/pull/*` przeżywają force-push).
   * Pilnuje tego `app.spec.ts`: sprawdza konkretny adres, nie samo „jest w domenie github.com".
   */
  protected readonly repoUrl = 'https://github.com/KubaKrychowski/budget-tracker';

  /**
   * Adres lokalnej instancji aplikacji.
   *
   * ⚠️ REWIZJA decyzji z issue #12 (2026-09-25, na prośbę właściciela). Landing celowo NIE miał
   * przycisku do aplikacji: bez hostingu i rejestracji prowadziłby donikąd, a strona obiecująca
   * coś, czego nie ma czym spełnić, szkodzi bardziej niż jej brak. Przycisk wraca, bo dziś ta
   * strona jest serwowana WYŁĄCZNIE lokalnie (`ng serve landing`), obok aplikacji na 4200 —
   * w tym jedynym użyciu link prowadzi dokładnie tam, gdzie zapowiada.
   *
   * ⚠️ REWIZJA (2026-09-25, wieczór): aplikacja stoi już na Static Web Apps, więc adres przestał być
   * lokalny. To był dokładnie ten moment, przed którym ostrzegała poprzednia wersja tego komentarza:
   * `localhost` u obcego czytelnika to jedyne CTA prowadzące donikąd.
   *
   * ⚠️ REWIZJA (2026-09-26): adres wskazywał host Static Web Apps. Działał, ale logowanie startowało
   * wtedy z NIEWŁAŚCIWEGO originu — klient SPA buduje `redirect_uri` z `window.location.origin`, więc
   * wejście przez stary host kończyło się powrotem na stary host, mimo że Identity zna już wyłącznie
   * `app.wydatki.com`.
   *
   * Adres jest wpisany na sztywno, bo landing nie ma konfiguracji wczytywanej w czasie działania
   * (aplikacja ma — patrz `core/runtime-config.ts`). Od przejścia na własną domenę jest to jednak adres
   * STABILNY, a nie losowy host, który trzeba było podmieniać po każdym odtworzeniu środowiska.
   */
  /**
   * Profil autora. ⚠️ Nazwisko celowo NIE pada w treści strony, ale ten link i adres repozytorium
   * i tak je ujawniają — to świadomy kompromis: sekcja „kto to napisał" bez możliwości sprawdzenia,
   * kto to napisał, nie ma sensu.
   */
  protected readonly linkedInUrl = 'https://www.linkedin.com/in/kuba-krychowski/';

  protected readonly appUrl = 'https://app.wydatki.com';

  /**
   * DOMYŚLNY próg pewności, poniżej którego transakcja idzie do przeglądu zamiast dostać kategorię.
   *
   * ⚠️ To kopia wartości z backendu (`MlCategorizer.ConfidenceThreshold`, domyślnie `0.7m`,
   * nadpisywalna w `appsettings`), a nie jej źródło — i NIC tego nie synchronizuje. Landing jest
   * osobnym projektem, więc nie ma tu testu, który złapałby zmianę progu po stronie API.
   * Dlatego tekst na stronie mówi „domyślnie”, zamiast podawać tę liczbę jako prawo: sam próg
   * jest w CLAUDE.md §9 opisany jako ZAŁOŻENIE do strojenia na realnych danych.
   *
   * Stoi na stronie mimo to, bo „model czasem się myli” bez liczby jest ogólnikiem,
   * a z liczbą jest sprawdzalnym opisem mechanizmu.
   */
  protected readonly reviewThreshold = 0.7;

  private readonly document = inject(DOCUMENT);

  /**
   * Adres serwera tożsamości, do którego idzie prośba o dostęp do bety.
   *
   * ⚠️ Na sztywno, z jednym wyjątkiem na lokalny dev — z tego samego powodu co `appUrl`: landing nie ma konfiguracji
   * wczytywanej w czasie działania. Przy renderowaniu po stronie serwera (prerender) `location` bywa puste, więc
   * pusty host to produkcja, a nie błąd.
   */
  private readonly identityUrl = computed(() =>
    this.document.location?.hostname === 'localhost' ? 'https://localhost:7226' : 'https://auth.wydatki.com');

  // ── Prośba o dostęp do bety ────────────────────────────────────────────────────────────

  protected readonly email = signal('');
  protected readonly consent = signal(false);

  /** Pułapka na boty: pole ukryte przed człowiekiem. Serwer traktuje niepuste jako automat. */
  protected readonly website = signal('');

  protected readonly requestState = signal<'idle' | 'sending' | 'done'>('idle');
  protected readonly emailError = signal<string | null>(null);
  protected readonly consentError = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);

  protected async requestAccess(): Promise<void> {
    if (this.requestState() === 'sending') return;

    const email = this.email().trim();
    this.emailError.set(/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) ? null : 'Podaj poprawny adres e-mail.');
    this.consentError.set(this.consent() ? null : 'Zgoda jest wymagana, żeby zapisać adres.');
    this.formError.set(null);
    if (this.emailError() || this.consentError()) return;

    this.requestState.set('sending');
    try {
      const response = await fetch(`${this.identityUrl()}/api/beta-requests`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, consent: true, website: this.website() }),
      });

      if (response.ok) {
        this.requestState.set('done');
        return;
      }

      this.requestState.set('idle');
      if (response.status === 429) {
        this.formError.set('Za dużo prób w krótkim czasie. Spróbuj za kilka minut.');
      } else if (response.status === 400) {
        // Serwer jest ostatecznym sędzią adresu — jego odmowa pokazuje się przy polu, nie jako „coś poszło nie tak".
        this.emailError.set('Podaj poprawny adres e-mail.');
      } else {
        this.formError.set('Nie udało się zapisać adresu. Spróbuj ponownie za chwilę.');
      }
    } catch {
      this.requestState.set('idle');
      this.formError.set('Nie udało się zapisać adresu. Sprawdź połączenie i spróbuj ponownie.');
    }
  }
}
