import { Component, computed, inject, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter, map } from 'rxjs';
import { TranslatePipe } from '@ngx-translate/core';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { ChangelogService } from '../changelog/changelog.service';
import { TerminalService } from '../terminal/terminal.service';

/** Pozycja dolnego paska: kolorowa ikona sekcji z `public/icons` — ta sama co w kafelkach i wyszukiwarce. */
interface TabItem {
  readonly route: string;
  readonly labelKey: string;
  readonly svg: string;
}

/** Pozycja arkusza „Więcej”: trasa albo akcja; ikona to plik z `public/icons` albo `nz-icon`. */
interface MoreItem {
  readonly id: string;
  readonly labelKey: string;
  readonly route?: string;
  readonly svg?: string;
  readonly icon?: string;
  readonly danger?: boolean;
}

/**
 * Dolny pasek nawigacji na telefonie (makieta Figma „Mobile — Dashboard”, strona „Mobile (propozycja)”).
 *
 * @remarks
 * Na pasku są ekrany, do których wraca się najczęściej z telefonu: dashboard, limity, cele i zlecenia stałe —
 * mobile służy do podglądu (DECISIONS.md §7). Reszta, razem z tym, co na desktopie siedzi w ikonach nagłówka
 * (terminal, ustawienia, wylogowanie, „Co nowego”), jest w arkuszu „Więcej”.
 */
@Component({
  selector: 'app-mobile-nav',
  imports: [RouterLink, RouterLinkActive, TranslatePipe, NzDrawerModule, NzIconModule],
  templateUrl: './mobile-nav.html',
  styleUrl: './mobile-nav.scss',
})
export class MobileNav {
  private readonly router = inject(Router);
  protected readonly changelog = inject(ChangelogService);
  private readonly terminal = inject(TerminalService);

  /** Wylogowanie robi powłoka (`App.logout`) — czyści też stan lokalny, więc nie dublujemy tej logiki tutaj. */
  readonly logout = output();

  protected readonly tabs: readonly TabItem[] = [
    { route: '/dashboard', labelKey: 'mobileNav.dashboard', svg: 'icons/dashboard-azure.svg' },
    { route: '/limits', labelKey: 'mobileNav.limits', svg: 'icons/limity-wydatkow-azure.svg' },
    { route: '/savings', labelKey: 'mobileNav.goals', svg: 'icons/cele-oszczednosciowe-azure.svg' },
    { route: '/standing-orders', labelKey: 'mobileNav.standingOrders', svg: 'icons/zlecenia-stale-azure.svg' },
  ];

  protected readonly moreItems: readonly MoreItem[] = [
    { id: 'transactions', labelKey: 'actions.transactionList', route: '/transactions', svg: 'icons/lista-transakcji-azure.svg' },
    { id: 'episodic', labelKey: 'actions.episodicOrders', route: '/episodic-orders', svg: 'icons/wydatki-epizodyczne-azure.svg' },
    { id: 'strategies', labelKey: 'actions.strategies', route: '/strategies', svg: 'icons/strategia-oszczedzania-azure.svg' },
    { id: 'import', labelKey: 'actions.importStatement', route: '/import', svg: 'icons/import-azure.svg' },
    { id: 'changelog', labelKey: 'changelog.open', icon: 'star' },
    { id: 'terminal', labelKey: 'terminal.openTerminal', icon: 'code' },
    { id: 'settings', labelKey: 'settings.openSettings', route: '/settings', icon: 'setting' },
    { id: 'logout', labelKey: 'auth.logout', icon: 'logout', danger: true },
  ];

  protected readonly moreOpen = signal(false);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** „Więcej” świeci się na każdym ekranie spoza paska — inaczej żadna pozycja nie byłaby aktywna. */
  protected readonly moreActive = computed(() => {
    const path = this.url().split(/[?#]/)[0];
    return !this.tabs.some((t) => path === t.route || path.startsWith(t.route + '/'));
  });

  protected select(item: MoreItem): void {
    this.moreOpen.set(false);
    if (item.route) {
      void this.router.navigateByUrl(item.route);
      return;
    }
    if (item.id === 'changelog') this.changelog.open(false);
    if (item.id === 'terminal') this.terminal.toggle();
    if (item.id === 'logout') this.logout.emit();
  }
}
