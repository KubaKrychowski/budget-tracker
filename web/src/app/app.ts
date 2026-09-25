import { Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { TranslatePipe } from '@ngx-translate/core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { handbookTopicKeyForRoute } from './core/handbook-topics';
import { RecentScreens } from './core/search/recent-screens';
import { SearchMenu } from './core/search-menu/search-menu';
import { Terminal } from './core/terminal/terminal';
import { TerminalService } from './core/terminal/terminal.service';

@Component({
  imports: [
    RouterLink, RouterOutlet, FormsModule,
    NzIconModule, TranslatePipe, SearchMenu, Terminal,
  ],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  private readonly router = inject(Router);
  private readonly oidcSecurityService = inject(OidcSecurityService);
  private readonly recentScreens = inject(RecentScreens);
  protected readonly terminal = inject(TerminalService);

  /** Wylogowanie po stronie klienta — kończy sesję też na serwerze tożsamości (RP-initiated logout). */
  protected logout(): void {
    this.oidcSecurityService.logoff().subscribe();
  }

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
