import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSegmentedModule } from 'ng-zorro-antd/segmented';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { StrategyResult } from '../../core/api/models/strategies';

/** Co rysuje wykres — te same trzy miary, które porównuje warunek na kafelku. */
export type CompareMetric = 'Cash' | 'Debt' | 'CashMinusDebt';

/** Jedna seria: wariant (albo bazowy, gdy `id` jest `null`) z wynikiem symulacji. */
export interface CompareSeries {
  readonly id: string | null;
  readonly name: string;
  readonly result: StrategyResult;
}

interface ChartLine {
  readonly name: string;
  readonly color: string;
  readonly dash: string | null;
  readonly points: string;
  readonly endX: number;
  readonly endY: number;
  readonly endLabel: string;
}

interface TableRow {
  readonly label: string;
  readonly cells: readonly { readonly value: string; readonly delta: string | null }[];
}

const WIDTH = 852;
const HEIGHT = 268;
const LEFT = 78;
const RIGHT = WIDTH - 36;
const TOP = 16;
const BOTTOM = HEIGHT - 36;
const TICKS = 4;

/** Kolory serii z tokenów aplikacji — pierwszy to zawsze bazowy; od szóstej serii linia jest przerywana. */
const COLORS = [
  'var(--ds-primary-600)', 'var(--ds-warn-500)', 'var(--ds-info-500)', 'var(--ds-error-500)', 'var(--ds-neutral-900)',
];

/** Najbliższy „ładny” krok osi (1, 2, 5 × potęga dziesięciu), który mieści zakres w `ticks` przedziałach. */
export function niceStep(range: number, ticks: number): number {
  if (range <= 0) return 1;
  const raw = range / ticks;
  const power = 10 ** Math.floor(Math.log10(raw));
  const fraction = raw / power;
  return (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * power;
}

/** Wartość serii w jednym miesiącu dla wybranej miary. */
export function metricValue(metric: CompareMetric, cash: number, debt: number): number {
  return metric === 'Cash' ? cash : metric === 'Debt' ? debt : cash - debt;
}

/**
 * Okno „Porównanie wariantów” (makieta Figma „Strategia”: 423:762) — gotówka, dług albo gotówka minus dług na koniec
 * każdego miesiąca dla wariantu bazowego i wszystkich wariantów, a pod wykresem tabela kluczowych dat i kwot z różnicą
 * względem bazowego. Bez wariantów to „Przebieg symulacji” jednej serii.
 *
 * Wykres to zwykłe SVG z tokenów aplikacji — biblioteka wykresów byłaby nową zależnością dla jednego okna. To SZACUNEK,
 * więc okno zaczyna się alertem (jak okno usuwania: alert zawsze pierwszym elementem treści).
 */
@Component({
  selector: 'app-strategy-compare',
  imports: [FormsModule, NzAlertModule, NzButtonModule, NzModalModule, NzSegmentedModule, TranslatePipe],
  templateUrl: './strategy-compare.html',
  styleUrl: './strategy-compare.scss',
})
export class StrategyCompare {
  private readonly translate = inject(TranslateService);

  readonly open = input(false);
  readonly series = input.required<readonly CompareSeries[]>();
  readonly hasLoan = input(false);
  readonly hasCushion = input(false);

  readonly closed = output<void>();

  protected readonly metric = signal<CompareMetric>('Cash');
  protected readonly width = WIDTH;
  protected readonly height = HEIGHT;
  protected readonly baseline = BOTTOM;
  protected readonly left = LEFT;
  protected readonly right = RIGHT;

  protected readonly metricOptions = computed(() =>
    (['Cash', 'Debt', 'CashMinusDebt'] as const).map((value) => ({ value, label: this.translate.instant(`strategies.compare.metrics.${value}`) })));

  protected readonly single = computed(() => this.series().length <= 1);

  protected readonly metricName = computed(() => this.translate.instant(`strategies.compare.metrics.${this.metric()}`));

  protected readonly chart = computed(() => {
    const series = this.series();
    const metric = this.metric();
    const values = series.map((s) => s.result.months.map((m) => metricValue(metric, m.cash, m.debt)));
    const all = values.flat();
    const min = Math.min(0, ...all);
    const max = Math.max(0, ...all);
    const step = niceStep(max - min, TICKS);
    const low = Math.floor(min / step) * step;
    const high = Math.max(Math.ceil(max / step) * step, low + step);
    const y = (v: number): number => BOTTOM - ((v - low) / (high - low)) * (BOTTOM - TOP);
    const months = series[0]?.result.months ?? [];
    const x = (i: number): number => (months.length <= 1 ? LEFT : LEFT + (i / (months.length - 1)) * (RIGHT - LEFT));

    const lines: ChartLine[] = series.map((s, i) => {
      const points = values[i].map((v, j) => `${x(j).toFixed(1)},${y(v).toFixed(1)}`);
      const last = values[i].at(-1) ?? 0;
      return {
        name: s.name,
        color: COLORS[i % COLORS.length],
        dash: i >= COLORS.length ? '6 4' : null,
        points: points.join(' '),
        endX: x(Math.max(values[i].length - 1, 0)),
        endY: y(last),
        endLabel: this.money(last),
      };
    });

    const yTicks: { y: number; label: string }[] = [];
    for (let v = low; v <= high + step / 2; v += step) yTicks.push({ y: y(v), label: this.money(v) });

    const every = Math.max(1, Math.ceil(months.length / 15));
    const xTicks = months
      .map((m, i) => ({ x: x(i), label: this.shortMonth(m.month), show: i % every === 0 }))
      .filter((t) => t.show);

    return { lines, yTicks, xTicks };
  });

  protected readonly rows = computed<readonly TableRow[]>(() => {
    const series = this.series();
    const base = series[0]?.result;
    const rows: TableRow[] = [];
    const never = this.translate.instant('strategies.compare.never');

    const dateRow = (label: string, pick: (r: StrategyResult) => string | null): TableRow => ({
      label,
      cells: series.map((s, i) => {
        const month = pick(s.result);
        const reference = base ? pick(base) : null;
        const months = i > 0 && month && reference ? this.monthsBetween(reference, month) : 0;
        return {
          value: month ? this.monthLabel(month) : never,
          delta: months === 0 ? null : this.translate.instant('strategies.compare.monthsShort', { delta: `${months > 0 ? '+' : '−'}${Math.abs(months)}` }),
        };
      }),
    });
    const amountRow = (label: string, pick: (r: StrategyResult) => number, prefix = ''): TableRow => ({
      label,
      cells: series.map((s, i) => {
        const value = pick(s.result);
        const diff = i > 0 && base ? value - pick(base) : 0;
        return { value: `${prefix}${this.money(value)}`, delta: diff === 0 ? null : `${diff > 0 ? '+' : '−'}${this.money(Math.abs(diff))}` };
      }),
    });

    if (this.hasLoan()) rows.push(dateRow(this.translate.instant('strategies.compare.loanPaidOff'), (r) => r.loanPaidOffIn));
    if (this.hasCushion()) rows.push(dateRow(this.translate.instant('strategies.compare.cushionReached'), (r) => r.cushionReachedIn));
    rows.push(amountRow(this.translate.instant('strategies.compare.finalCash'), (r) => r.finalCash));
    if (this.hasLoan()) rows.push(amountRow(this.translate.instant('strategies.compare.interest'), (r) => r.totalInterest));
    return rows;
  });

  protected color(index: number): string {
    return COLORS[index % COLORS.length];
  }

  protected dash(index: number): string | null {
    return index >= COLORS.length ? '6 4' : null;
  }

  private locale(): string {
    return this.translate.currentLang() || 'pl';
  }

  private money(value: number): string {
    return `${new Intl.NumberFormat(this.locale(), { maximumFractionDigits: 0 }).format(value)} zł`;
  }

  private monthLabel(month: string): string {
    const [year, m] = month.split('-').map(Number);
    return new Intl.DateTimeFormat(this.locale(), { month: 'long', year: 'numeric' }).format(new Date(year, m - 1, 1));
  }

  private shortMonth(month: string): string {
    const [year, m] = month.split('-').map(Number);
    return new Intl.DateTimeFormat(this.locale(), { month: 'short' }).format(new Date(year, m - 1, 1));
  }

  private monthsBetween(from: string, to: string): number {
    const [fy, fm] = from.split('-').map(Number);
    const [ty, tm] = to.split('-').map(Number);
    return (ty - fy) * 12 + (tm - fm);
  }
}
