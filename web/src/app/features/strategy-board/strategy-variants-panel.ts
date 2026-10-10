import { Component, computed, inject, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { StrategyResult, StrategyVariant } from '../../core/api/models/strategies';
import { canAddVariant } from './strategy-variants';

/** Kafelek na liście przełączników — tytuł do pokazania, nie cały węzeł. */
export interface VariantTile {
  readonly id: string;
  readonly title: string;
}

/** Karta wariantu z gotowym tekstem wyniku — szablon nie liczy niczego sam. */
interface VariantCard {
  readonly id: string | null;
  readonly name: string;
  readonly loan: string | null;
  readonly cash: string;
  readonly delta: string | null;
}

/**
 * Zakładka „Warianty” panelu bocznego tablicy strategii (makieta Figma „Strategia”: warianty, 423:451).
 *
 * Wariant to ten sam graf z wyłączonymi kafelkami: karta pokazuje jego wynik (spłata kredytu, gotówka na koniec i różnicę
 * względem bazowego), a przełączniki włączają i wyłączają kafelki WYBRANEGO wariantu. „Bazowy” ma wszystkie kafelki
 * włączone i nie da się go edytować ani usunąć — istnieje zawsze.
 *
 * Komponent nie zmienia stanu — wszystko, co użytkownik zrobi, wraca do tablicy jako zdarzenie, bo tylko ona trzyma graf
 * i warianty (jedno źródło prawdy dla „Odrzuć zmiany” i symulacji).
 */
@Component({
  selector: 'app-strategy-variants-panel',
  imports: [FormsModule, NzSwitchModule, TranslatePipe],
  templateUrl: './strategy-variants-panel.html',
  styleUrl: './strategy-variants-panel.scss',
})
export class StrategyVariantsPanel {
  private readonly translate = inject(TranslateService);

  readonly variants = input.required<readonly StrategyVariant[]>();
  readonly activeId = input<string | null>(null);
  readonly baseResult = input<StrategyResult | null>(null);
  /** Wyniki wariantów po identyfikatorze — brak wpisu znaczy, że symulacja jeszcze się liczy. */
  readonly results = input<Readonly<Record<string, StrategyResult>>>({});
  readonly tiles = input<readonly VariantTile[]>([]);
  /** Czy na tablicy jest kredyt — bez niego linia „kredyt spłacony” nic by nie mówiła. */
  readonly hasLoan = input(false);

  readonly picked = output<string | null>();
  readonly added = output<void>();
  readonly duplicated = output<string>();
  readonly renamed = output<string>();
  readonly removed = output<string>();
  readonly toggled = output<{ readonly nodeId: string; readonly disabled: boolean }>();

  protected readonly canAdd = computed(() => canAddVariant(this.variants()));

  protected readonly active = computed(() => this.variants().find((v) => v.id === this.activeId()) ?? null);

  protected readonly cards = computed<readonly VariantCard[]>(() => {
    const base = this.baseResult();
    const list: VariantCard[] = [this.card(null, this.translate.instant('strategies.variants.base'), base, null)];
    for (const v of this.variants()) list.push(this.card(v.id, v.name, this.results()[v.id] ?? null, base));
    return list;
  });

  protected isOff(nodeId: string): boolean {
    return this.active()?.disabledNodeIds.includes(nodeId) ?? false;
  }

  private locale(): string {
    return this.translate.currentLang() || 'pl';
  }

  private card(id: string | null, name: string, result: StrategyResult | null, base: StrategyResult | null): VariantCard {
    if (!result) return { id, name, loan: null, cash: '', delta: null };
    const loan = this.hasLoan()
      ? this.translate.instant('strategies.variants.loan', {
          month: result.loanPaidOffIn ? this.monthLabel(result.loanPaidOffIn) : this.translate.instant('strategies.variants.never'),
        })
      : null;
    const delta = id !== null && base ? result.finalCash - base.finalCash : 0;
    return {
      id,
      name,
      loan,
      cash: this.translate.instant('strategies.variants.cash', { amount: this.money(result.finalCash) }),
      delta: id !== null && base && delta !== 0
        ? this.translate.instant('strategies.variants.delta', { delta: `${delta > 0 ? '+' : '−'}${this.money(Math.abs(delta))}` })
        : null,
    };
  }

  private money(value: number): string {
    return `${new Intl.NumberFormat(this.locale(), { maximumFractionDigits: 0 }).format(value)} zł`;
  }

  private monthLabel(month: string): string {
    const [year, m] = month.split('-').map(Number);
    return new Intl.DateTimeFormat(this.locale(), { month: 'long', year: 'numeric' }).format(new Date(year, m - 1, 1));
  }
}
