import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { NzAutocompleteModule, NzOptionSelectionChange } from 'ng-zorro-antd/auto-complete';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { SYSTEM_ACTIONS } from './core/system-actions';
import { SystemAction } from './core/models/system-action';
import { normalizeText } from './core/normalize-text';
import { handbookTopicKeyForRoute } from './core/handbook-topics';
import { Terminal } from './core/terminal/terminal';
import { TerminalService } from './core/terminal/terminal.service';

@Component({
  imports: [
    RouterLink, RouterOutlet, FormsModule,
    NzAutocompleteModule, NzInputModule, NzIconModule, TranslatePipe, Terminal,
  ],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  private readonly message = inject(NzMessageService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly oidcSecurityService = inject(OidcSecurityService);
  protected readonly terminal = inject(TerminalService);
  protected readonly query = signal('');

  /** Wylogowanie po stronie klienta — kończy sesję też na serwerze tożsamości (RP-initiated logout). */
  protected logout(): void {
    this.oidcSecurityService.logoff().subscribe();
  }

  /**
   * Tłumaczenia ładują się asynchronicznie (HTTP). Bez tej zależności `computed`
   * policzyłby się raz, na surowych kluczach, i wyszukiwarka nigdy nie zaczęłaby
   * dopasowywać po widocznych etykietach.
   */
  private readonly langLoaded = toSignal(this.translate.onLangChange, { initialValue: null });

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

  /** Podpowiedzi filtrowane bez uwzględniania wielkości liter i polskich ogonków. */
  protected readonly suggestions = computed(() => {
    this.langLoaded();
    const q = normalizeText(this.query());
    if (!q || q.length < 3) return [];
    return SYSTEM_ACTIONS.filter((a) => normalizeText(this.translate.instant(a.labelKey)).includes(q));
  });

  /**
   * `selectionChange` leci również wtedy, gdy autocomplete otwiera listę i oznacza
   * opcje jako aktywne — bez `isUserInput` komunikat pokazywał się dla każdej
   * podpowiedzi natychmiast po wejściu w pole, zamiast po wyborze konkretnej.
   */
  protected select(event: NzOptionSelectionChange, action: SystemAction): void {
    if (!event.isUserInput) return;

    if (action.route) {
      this.query.set('');
      void this.router.navigate([action.route]);
      return;
    }

    // Akcje bez trasy są w szablonie wygaszone (`nzDisabled`), więc normalnie tu nie
    // dojdziemy. Gałąź zostaje jako zabezpieczenie: dopisanie akcji bez trasy powie
    // o tym wprost, zamiast po cichu nic nie zrobić.
    this.query.set('');
    this.message.info(
      this.translate.instant('actions.notImplemented', {
        label: this.translate.instant(action.labelKey),
      }),
    );
  }
}
