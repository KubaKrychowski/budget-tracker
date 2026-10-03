import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSegmentedModule } from 'ng-zorro-antd/segmented';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { parseAmount } from '../../core/parse-amount';
import {
  OverpaymentMode, StrategyConditionComparison, StrategyConditionMetric, StrategyNode, StrategyNodeOutcome,
  StrategyProblemKind,
} from '../../core/api/models/strategies';
import { NODE_META, NodeField } from './strategy-node-meta';

/** Najdłuższy podpis kafelka — ten sam co `StrategyGraphValidator` w API. */
const MAX_TITLE_LENGTH = 100;

/**
 * Ustawienia zaznaczonego kafelka (makieta Figma „Strategia — ustawienia zaznaczonego węzła”, 380:415, 385:246,
 * 385:532, 385:812) — zastępuje paletę w prawym panelu tablicy.
 *
 * Komponent nie zapisuje niczego sam: każda zmiana pola wraca do rodzica jako CAŁY nowy kafelek (`changed`), a rodzic
 * trzyma stan tablicy. Dzięki temu „Odrzuć zmiany” i symulacja na bieżąco działają na jednym źródle prawdy.
 */
@Component({
  selector: 'app-strategy-node-form',
  imports: [
    DatePipe, FormsModule, NzButtonModule, NzDatePickerModule, NzInputModule, NzInputNumberModule, NzSegmentedModule,
    NzSelectModule, TranslatePipe,
  ],
  templateUrl: './strategy-node-form.html',
  styleUrl: './strategy-node-form.scss',
})
export class StrategyNodeForm {
  /** Parser polskiego formatu kwot dla pól `nz-input-number` — uzasadnienie przy `parseAmount`. */
  protected readonly parseAmount = parseAmount;
  protected readonly maxTitleLength = MAX_TITLE_LENGTH;

  private readonly translate = inject(TranslateService);

  readonly node = input.required<StrategyNode>();

  /** Co symulacja zrobiła z tym węzłem; `null`, gdy się nie wykonał albo symulacja jeszcze nie wróciła. */
  readonly outcome = input<StrategyNodeOutcome | null>(null);

  /** Problemy tego węzła do pokazania pod polami. */
  readonly problems = input<readonly StrategyProblemKind[]>([]);

  readonly changed = output<StrategyNode>();
  readonly removed = output<void>();
  readonly closed = output<void>();

  protected readonly meta = computed(() => NODE_META[this.node().type]);

  protected readonly modes: readonly OverpaymentMode[] = ['ReduceInstallment', 'ShortenPeriod'];
  protected readonly metrics: readonly StrategyConditionMetric[] = ['Cash', 'Debt', 'CashMinusDebt'];
  protected readonly comparisons: readonly StrategyConditionComparison[] = ['AtLeast', 'AtMost'];

  /** Język nazw miesięcy — `DatePipe` bez tego pokazuje angielskie. */
  protected locale(): string {
    return this.translate.currentLang() || 'pl';
  }

  protected has(field: NodeField): boolean {
    return this.meta().fields.includes(field);
  }

  protected patch(change: Partial<StrategyNode>): void {
    this.changed.emit({ ...this.node(), ...change });
  }

  /**
   * Miesiąc węzła jako data do pola `nz-date-picker` (pierwszy dzień miesiąca).
   *
   * ⚠️ MUSI być `computed`, nie metoda: `[ngModel]` porównuje wartość przez `===`, a metoda zwracałaby nową instancję
   * `Date` przy każdym renderze. Pole widziałoby wtedy „zmianę”, emitowało `ngModelChange`, to aktualizowało tablicę,
   * formularz renderował się od nowa — i tak w nieskończoność (renderer przeglądarki się wywracał).
   */
  protected readonly monthDate = computed(() => {
    const month = this.node().month;
    if (!month) return null;
    const [year, m] = month.split('-').map(Number);
    return new Date(year, m - 1, 1);
  });

  protected setMonth(date: Date | null): void {
    this.patch({ month: date ? this.isoMonth(date) : null });
  }

  protected setMode(index: number): void {
    this.patch({ mode: this.modes[index] ?? null });
  }

  protected modeIndex(): number {
    return Math.max(0, this.modes.indexOf(this.node().mode ?? 'ReduceInstallment'));
  }

  private isoMonth(date: Date): string {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-01`;
  }
}
