import { Component, ElementRef, HostListener, computed, effect, inject, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { TranslatePipe } from '@ngx-translate/core';
import { ActiveBudget } from '../active-budget';
import { SearchGroup, SearchHit, SearchKind, SearchResponse } from '../api/models/search';
import { errorOf, valueOf } from '../api/resource-value';
import { SystemAction } from '../models/system-action';
import { QUICK_ACTIONS, SYSTEM_ACTIONS } from '../system-actions';
import { RecentScreens } from '../search/recent-screens';
import { SearchFocus } from '../search/search-focus';
import { SearchHistory } from '../search/search-history';

/** Krótsza fraza pasuje do wszystkiego — ta sama granica co po stronie serwera. */
const MIN_QUERY_LENGTH = 2;

/**
 * Odstęp między ostatnim klawiszem a żądaniem. Bez niego „catering" to osiem zapytań,
 * z których siedem jest nieaktualnych, zanim wrócą.
 */
const DEBOUNCE_MS = 200;

/**
 * Wyszukiwarka z nagłówka wraz z rozwijanym menu (makieta „Wyszukiwarka i ostatnie akcje”).
 *
 * <p>Zastąpiła `nz-autocomplete`, który szukał WYŁĄCZNIE wśród akcji systemu i odzywał się dopiero
 * od trzeciego znaku. Menu otwiera się już po kliknięciu w puste pole — bo najczęstsza potrzeba to
 * „wróć tam, gdzie byłem", a nie „znajdź coś nowego".</p>
 *
 * <p>⚠️ Zakres jest WIDOCZNY w stopce. Transakcje z konta oszczędnościowego leżą w osobnym budżecie,
 * więc pusty wynik bez podanego zakresu wygląda jak awaria, a nie jak świadome zawężenie.</p>
 */
@Component({
  selector: 'app-search-menu',
  imports: [FormsModule, NzIconModule, NzInputModule, TranslatePipe],
  templateUrl: './search-menu.html',
  styleUrl: './search-menu.scss',
})
export class SearchMenu {
  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly activeBudget = inject(ActiveBudget);
  private readonly searchFocus = inject(SearchFocus);
  protected readonly history = inject(SearchHistory);
  protected readonly recent = inject(RecentScreens);

  protected readonly query = signal('');
  protected readonly open = signal(false);
  protected readonly allBudgets = signal(false);

  /** Fraza, na którą faktycznie pytamy serwer — opóźniona względem pisania. */
  private readonly debounced = signal('');

  private readonly debounce = effect((onCleanup) => {
    const value = this.query();
    const id = setTimeout(() => this.debounced.set(value), DEBOUNCE_MS);
    onCleanup(() => clearTimeout(id));
  });

  private readonly resource = httpResource<SearchResponse>(() => {
    const q = this.debounced().trim();
    if (q.length < MIN_QUERY_LENGTH) return undefined;
    const budgetId = this.activeBudget.resolve([])[0];
    return {
      url: '/api/search',
      params: {
        q,
        ...(this.allBudgets() ? { allBudgets: 'true' } : budgetId ? { budgetId } : {}),
      },
    };
  });

  /** `value()` RZUCA w stanie błędu — patrz core/api/resource-value.ts. */
  private readonly value = valueOf(this.resource);

  protected readonly loading = this.resource.isLoading;
  protected readonly failure = errorOf(this.resource);

  /**
   * Wynik pokazujemy tylko wtedy, gdy dotyczy TEGO, co widać w polu.
   *
   * ⚠️ Bez tego porównania po zawężeniu frazy przez chwilę wiszą trafienia do poprzedniej —
   * użytkownik widzi wyniki dla „cater", mając wpisane „catering", i nie ma jak tego odróżnić.
   */
  protected readonly data = computed(() => {
    const result = this.value();
    return result && result.query === this.query().trim() ? result : null;
  });

  protected readonly searching = computed(() => this.query().trim().length >= MIN_QUERY_LENGTH);
  protected readonly groups = computed<SearchGroup[]>(() => this.data()?.groups ?? []);
  protected readonly nothingFound = computed(() =>
    this.searching() && !this.loading() && this.data() !== null && this.groups().length === 0);

  /** Podpowiedzi dla kogoś, kto jeszcze nigdzie nie był — pierwsze kroki zamiast pustej karty. */
  protected readonly firstSteps = QUICK_ACTIONS;

  /** Akcje pasujące do frazy — szukamy po ETYKIETACH, więc wynik zależy od języka interfejsu. */
  protected readonly actionHits = computed<SystemAction[]>(() => {
    const q = this.query().trim().toLowerCase();
    if (q.length < MIN_QUERY_LENGTH) return [];
    return SYSTEM_ACTIONS.filter((a) => a.key.includes(q) || a.labelKey.toLowerCase().includes(q));
  });

  // ── Otwieranie i zamykanie ───────────────────────────────────────────────────────────

  protected focus(): void {
    this.open.set(true);
  }

  /**
   * Otwarcie na prośbę z zewnątrz („Wszystkie akcje" na dashboardzie).
   *
   * Pierwsze wywołanie pomijamy — `effect` leci raz na starcie, a menu rozwijające się samo
   * przy wejściu na stronę zasłaniałoby treść, o którą nikt nie prosił.
   */
  private readonly openOnRequest = effect(() => {
    if (this.searchFocus.requests() === 0) return;
    this.open.set(true);
    this.host.nativeElement.querySelector('input')?.focus();
  });

  /**
   * Klik poza komponentem zamyka menu.
   *
   * ⚠️ Na `document`, nie na `blur` pola. `blur` leci ZANIM klik dojdzie do wiersza wyniku,
   * więc menu znikałoby, zanim zdąży obsłużyć wybór.
   */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    if (!this.host.nativeElement.contains(event.target as Node)) this.open.set(false);
  }

  protected onEscape(): void {
    this.open.set(false);
  }

  protected clearHistory(): void {
    this.history.clear();
  }

  protected toggleAllBudgets(): void {
    this.allBudgets.update((v) => !v);
  }

  // ── Wybór ────────────────────────────────────────────────────────────────────────────

  protected pickHistory(entry: string): void {
    this.query.set(entry);
  }

  protected pickScreen(action: SystemAction): void {
    if (!action.route) return;
    this.close();
    void this.router.navigate([action.route]);
  }

  /** Enter bez wyboru konkretnego wiersza = „pokaż mi wszystko, co pasuje". */
  protected submit(): void {
    const q = this.query().trim();
    if (q.length < MIN_QUERY_LENGTH) return;
    this.showAll();
  }

  protected showAll(): void {
    const q = this.query().trim();
    this.history.add(q);
    this.close();
    void this.router.navigate(['/transactions'], { queryParams: this.budgetParams({ search: q }) });
  }

  /**
   * Przejście do trafienia.
   *
   * Kategoria prowadzi na listę transakcji, a nie na limity: kategoria bez ustawionego limitu
   * nie ma tam własnego wiersza, więc ekran limitów bywałby ślepym zaułkiem.
   */
  protected pickHit(kind: SearchKind, hit: SearchHit): void {
    this.history.add(this.query().trim());
    this.close();

    switch (kind) {
      case 'transactions':
        void this.router.navigate(['/transactions'], {
          queryParams: this.budgetParams({ search: hit.label }, hit.budgetId),
        });
        return;
      case 'categories':
        void this.router.navigate(['/transactions'], {
          queryParams: this.budgetParams({ categoryId: hit.id }),
        });
        return;
      case 'budgets':
        void this.router.navigate(['/dashboard'], { queryParams: { budgetId: hit.id } });
        return;
      case 'standingOrders':
        void this.router.navigate(['/standing-orders'], {
          queryParams: this.budgetParams({}, hit.budgetId),
        });
        return;
      case 'episodicOrders':
        void this.router.navigate(['/episodic-orders'], {
          queryParams: this.budgetParams({}, hit.budgetId),
        });
        return;
    }
  }

  private budgetParams(base: Record<string, string>, budgetId?: string | null): Record<string, string> {
    const id = budgetId ?? this.data()?.budgetId ?? this.activeBudget.resolve([])[0];
    return id ? { ...base, budgetId: id } : base;
  }

  private close(): void {
    this.open.set(false);
    this.query.set('');
    this.debounced.set('');
  }

  // ── Formatowanie ─────────────────────────────────────────────────────────────────────

  /** Klucz tłumaczenia nagłówka grupy — nazwy grup nie mogą przyjść z serwera (CLAUDE.md). */
  protected groupLabelKey(kind: SearchKind): string {
    return `search.groups.${kind}`;
  }

  protected money(value: number | null): string {
    if (value === null) return '';
    return new Intl.NumberFormat('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
      .format(Math.abs(value));
  }

  protected day(iso: string | null): string {
    if (!iso) return '';
    const [year, month, date] = iso.split('-');
    return `${date}.${month}.${year}`;
  }
}
