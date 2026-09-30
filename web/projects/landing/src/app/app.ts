import { DOCUMENT } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';

/**
 * Landing — strona główna dla zwykłego użytkownika (makieta Figma: „Landing — strona główna v3, od produktu”).
 *
 * <para>
 * Adresatem jest ktoś, kto chce wiedzieć, na co idą jego pieniądze, a nie ktoś oceniający warsztat. Stąd prosty
 * język, prawdziwe zrzuty ekranu i jedno wezwanie do działania: prośba o dostęp do zamkniętej bety. Szczegóły
 * techniczne żyją w repozytorium i dzienniku decyzji (jedno zdanie w sekcji „Kto za tym stoi”).
 * </para>
 *
 * <para>
 * ⚠️ Cała treść jest tu na sztywno po polsku, wbrew konwencji aplikacji (teksty w `public/i18n/*.json`). To
 * świadome odstępstwo: landing jest jednojęzyczny z założenia, a proza przepuszczona przez klucze tłumaczeń
 * staje się listą identyfikatorów, której nie da się ocenić w recenzji. Gdy dojdzie druga wersja językowa,
 * to się zmienia.
 * </para>
 *
 * <para>
 * ⚠️ Uczciwość jest wymogiem, nie ozdobą: strona nie obiecuje funkcji, których nie ma (łączenia z bankiem,
 * czytania paragonów, prognoz). Sekcja „Czego jeszcze nie ma” i testy w `app.spec.ts` tego pilnują.
 * </para>
 *
 * <para>
 * Sekcje NIE są osobnymi komponentami: to dokument liniowy bez stanu i powtórzeń (poza formularzem). Jeśli
 * któraś zacznie żyć własnym życiem, wtedy się ją wyciągnie.
 * </para>
 */
@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  /**
   * Publiczne repozytorium — dla programistów, w sekcji „Kto za tym stoi”.
   *
   * ⚠️ Musi wskazywać repozytorium **publiczne**. Do 2026-09-12 stał tu adres repo prywatnego, czyli link dawał
   * 404 każdemu odwiedzającemu. Pilnuje tego `app.spec.ts`: sprawdza konkretny adres, nie samo „jest w domenie
   * github.com".
   */
  protected readonly repoUrl = 'https://github.com/KubaKrychowski/budget-tracker';

  /**
   * Profil autora. ⚠️ Nazwisko celowo NIE pada w treści strony, ale ten link i adres repozytorium i tak je
   * ujawniają — to świadomy kompromis: sekcja „kto za tym stoi” bez możliwości sprawdzenia, kim jest autor,
   * nie ma sensu.
   */
  protected readonly linkedInUrl = 'https://www.linkedin.com/in/kuba-krychowski/';

  /**
   * Adres aplikacji — dla osób z zaproszeniem („Zaloguj się”).
   *
   * Wpisany na sztywno, bo landing nie ma konfiguracji wczytywanej w czasie działania (aplikacja ma — patrz
   * `core/runtime-config.ts`). Jest to adres STABILNY (własna domena), a nie losowy host Static Web Apps,
   * który trzeba było podmieniać po każdym odtworzeniu środowiska. ⚠️ Logowanie startuje z originu,
   * na którym wylądował użytkownik — wejście przez inny host kończyłoby się powrotem na ten host, a Identity
   * zna wyłącznie `app.wydatki.com`.
   */
  protected readonly appUrl = 'https://app.wydatki.com';

  private readonly document = inject(DOCUMENT);

  /**
   * Adres serwera tożsamości, do którego idzie prośba o dostęp do bety.
   *
   * ⚠️ Na sztywno, z jednym wyjątkiem na lokalny dev — z tego samego powodu co `appUrl`. Przy renderowaniu po
   * stronie serwera (prerender) `location` bywa puste, więc pusty host to produkcja, a nie błąd.
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
