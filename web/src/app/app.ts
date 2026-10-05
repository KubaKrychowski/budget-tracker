import { Component, afterNextRender, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { TranslatePipe } from '@ngx-translate/core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthSplash } from './core/auth-splash/auth-splash';
import { AuthSplashScreen } from './core/auth-splash/auth-splash-screen';
import { ChangelogService } from './core/changelog/changelog.service';
import { handbookTopicKeyForRoute } from './core/handbook-topics';
import { RecentScreens } from './core/search/recent-screens';
import { SearchMenu } from './core/search-menu/search-menu';
import { Terminal } from './core/terminal/terminal';
import { TerminalService } from './core/terminal/terminal.service';
import { MobileNav } from './core/mobile-nav/mobile-nav';
import { Viewport } from './core/layout/viewport';
import { AuthNavigation } from './core/native/auth-navigation';

@Component({
  imports: [
    RouterLink, RouterOutlet, FormsModule,
    NzIconModule, TranslatePipe, SearchMenu, Terminal, AuthSplashScreen, MobileNav,
  ],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  private readonly router = inject(Router);
  private readonly oidcSecurityService = inject(OidcSecurityService);
  private readonly authNavigation = inject(AuthNavigation);
  private readonly recentScreens = inject(RecentScreens);
  protected readonly terminal = inject(TerminalService);

  /** Telefon: dolny pasek nawigacji zamiast ikon nagłówka (makiety Figma „Mobile (propozycja)”). */
  protected readonly viewport = inject(Viewport);
  protected readonly changelog = inject(ChangelogService);

  /** Okładka na czas logowania — patrz `AuthSplash`. Powłoka pod nią jest `inert`, więc fokus nie ucieka za okładkę. */
  protected readonly splash = inject(AuthSplash);

  /** Statyczna okładka z `index.html` służyła tylko do momentu, aż Angular narysuje własną (albo uzna ją za zbędną). */
  private readonly loadChangelog = afterNextRender(() => this.changelog.load());

  private readonly removeBootSplash = afterNextRender(() => document.getElementById('boot-splash')?.remove());

  /**
   * Wylogowanie po stronie klienta — kończy sesję też na serwerze tożsamości (RP-initiated logout).
   *
   * ⚠️ Najpierw czyścimy własny stan w localStorage, DOPIERO potem wołamy `logoff()` (które przekierowuje
   * na Identity). Klucze aplikacji nie mają prefiksu konta, więc na współdzielonym urządzeniu następny
   * użytkownik zobaczyłby historię wyszukiwań/komend i aktywny budżet poprzednika. Czyszczenie przed
   * przekierowaniem gwarantuje, że wykona się niezależnie od tego, jak szybko odejdzie nawigacja.
   */
  protected logout(): void {
    this.clearLocalAppState();
    this.authNavigation.signOut();
  }

  /**
   * Usuwa z localStorage klucze należące do aplikacji (prefiksy `bt.`, `budget-tracker:`, `rules.`).
   * Prefiksowo, nie po jawnej liście — nowy klucz pod znanym prefiksem znika bez dopisywania go tutaj.
   * Owinięte w try/catch: w trybie prywatnym albo przy zablokowanym storage wylogowanie ma iść dalej.
   */
  private clearLocalAppState(): void {
    try {
      const prefixes = ['bt.', 'budget-tracker:', 'rules.'];
      const toRemove: string[] = [];
      for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (key && prefixes.some((p) => key.startsWith(p))) toRemove.push(key);
      }
      toRemove.forEach((k) => localStorage.removeItem(k));
    } catch {
      // localStorage niedostępny — nie blokujemy wylogowania.
    }
  }

  /** Zalogowany użytkownik z id_tokenu (`userData$`) — z niego bierzemy `sub` do rozpoznania zmiany konta. */
  private readonly userData = toSignal(this.oidcSecurityService.userData$, { initialValue: null });

  /**
   * Klucz właściciela zapamiętanego stanu. ⚠️ POZA prefiksami czyszczonymi przez `clearLocalAppState()`
   * (`bt-`, nie `bt.`), żeby przetrwał koniec sesji, który nie przeszedł przez przycisk wylogowania.
   */
  private static readonly OwnerKey = 'bt-owner';

  /**
   * Czyści ślady poprzedniego konta, gdy `sub` z tokenu różni się od zapamiętanego właściciela.
   *
   * ⚠️ `logout()` czyści stan tylko przy wylogowaniu PRZYCISKIEM. Gdy sesja skończy się inaczej
   * (wylogowanie bezpośrednio w Identity, w innej karcie, po wygaśnięciu) i na tym samym urządzeniu
   * zaloguje się inne konto, tamto czyszczenie się nie wykonało — tu domykamy tę lukę na starcie.
   * Klucze aplikacji nie mają prefiksu konta, więc bez tego nowy użytkownik zobaczyłby historię
   * wyszukiwań/komend i aktywny budżet poprzednika.
   */
  private readonly clearTracesOnAccountChange = effect(() => {
    const sub = (this.userData()?.userData as { sub?: string } | undefined)?.sub;
    if (!sub) return;
    try {
      if (localStorage.getItem(App.OwnerKey) !== sub) {
        this.clearLocalAppState();
        localStorage.setItem(App.OwnerKey, sub);
      }
    } catch {
      // localStorage niedostępny — nic nie czyścimy, ale też nic nie blokujemy.
    }
  });

  /** Okno „Co nowego" wyskakuje samo raz na sesję — dopiero gdy okładka logowania zniknie i jest sesja. */
  private autoOpened = false;
  private readonly openChangelogAfterUpdate = effect(() => {
    if (this.autoOpened || this.splash.visible() || !this.oidcSecurityService.authenticated().isAuthenticated) return;
    if (!this.changelog.hasUnread()) return;
    this.autoOpened = true;
    this.changelog.open(true);
  });

  /**
   * Temat podręcznika dopasowany do bieżącej trasy (issue #19) — ikona w nagłówku otwiera
   * podręcznik OD RAZU na temacie ekranu, z którego wychodzisz, zamiast zawsze od pierwszego
   * z listy. `toSignal` na `NavigationEnd`, bo `router.url` samo w sobie nie jest sygnałem.
   */
  private readonly navigationEnd = toSignal(
    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd)),
    { initialValue: null },
  );
  protected readonly handbookTopic = computed(() => {
    this.navigationEnd();
    return handbookTopicKeyForRoute(this.router.url);
  });

  /**
   * Odnotowanie odwiedzonego ekranu dla kafli „Ostatnie akcje" i sekcji „Ostatnie ekrany".
   *
   * Tutaj, a nie w każdym ekranie z osobna: powłoka i tak nasłuchuje nawigacji dla podręcznika,
   * a rozsypanie tego po komponentach znaczyłoby, że nowy ekran po cichu wypada z historii.
   */
  private readonly trackVisited = effect(() => {
    if (this.navigationEnd() === null) return;
    this.recentScreens.track(this.router.url);
  });
}
