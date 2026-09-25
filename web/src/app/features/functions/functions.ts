import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { FunctionGroup, SystemAction } from '../../core/models/system-action';
import { FUNCTION_GROUPS, SYSTEM_ACTIONS } from '../../core/system-actions';
import { normalizeText } from '../../core/normalize-text';
import { TerminalService } from '../../core/terminal/terminal.service';

/** Sekcja katalogu gotowa do wyświetlenia. */
interface CatalogGroup {
  readonly id: FunctionGroup;
  readonly actions: readonly SystemAction[];
}

/**
 * „Wszystkie funkcje" — katalog ekranów pogrupowany po tym, PO CO się do nich wchodzi
 * (makieta „Wyszukiwarka i ostatnie akcje", ramka 6). Odpowiednik „All services" z Azure.
 *
 * <p>Ekran istnieje, bo wyszukiwarka odpowiada na pytanie „gdzie jest TO, czego szukam",
 * a nie „co ta aplikacja w ogóle potrafi". Drugie pytanie zadaje się rzadko, ale wtedy
 * lista kafli z ostatnio odwiedzonymi jest bezużyteczna.</p>
 */
@Component({
  selector: 'app-functions',
  imports: [FormsModule, RouterLink, NzBreadCrumbModule, NzIconModule, NzInputModule, TranslatePipe],
  templateUrl: './functions.html',
  styleUrl: './functions.scss',
})
export class Functions {
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly terminal = inject(TerminalService);

  protected readonly filter = signal('');

  /**
   * Katalog nie wymienia SAM SIEBIE — wpis „Wszystkie funkcje" prowadzący na ten sam ekran
   * byłby ślepą pętlą. W rejestrze akcji zostaje, żeby dało się tu trafić z wyszukiwarki.
   */
  private readonly catalogActions = SYSTEM_ACTIONS.filter((a) => a.key !== 'all-functions');

  protected readonly groups = computed<CatalogGroup[]>(() => {
    const needle = normalizeText(this.filter().trim());
    const matches = (action: SystemAction): boolean => {
      if (!needle) return true;
      // Filtrujemy po tym, co widać: etykiecie i opisie — nie po kluczu tłumaczenia.
      const label = normalizeText(this.translate.instant(action.labelKey));
      const description = normalizeText(this.translate.instant(this.descriptionKey(action)));
      return label.includes(needle) || description.includes(needle);
    };

    return FUNCTION_GROUPS
      .map((id) => ({ id, actions: this.catalogActions.filter((a) => a.group === id && matches(a)) }))
      .filter((g) => g.actions.length > 0);
  });

  protected readonly nothingMatches = computed(() => this.groups().length === 0);

  /** Opis pod nazwą — mówi, co na tym ekranie zrobisz, a nie powtarza nazwy. */
  protected descriptionKey(action: SystemAction): string {
    return `functions.descriptions.${action.key}`;
  }

  protected groupLabelKey(id: FunctionGroup): string {
    return `functions.groups.${id}`;
  }

  /**
   * Terminal pokazujemy w katalogu, ale NIE ma go w rejestrze akcji: to panel nad bieżącym
   * widokiem, nie trasa. Wpis bez trasy w rejestrze byłby wygaszony wszędzie tam, gdzie
   * rejestr służy do nawigacji — a terminal działa.
   */
  protected openTerminal(): void {
    this.terminal.open();
  }

  protected open(action: SystemAction): void {
    if (!action.route) return;
    void this.router.navigate([action.route], { queryParams: action.queryParams });
  }
}
