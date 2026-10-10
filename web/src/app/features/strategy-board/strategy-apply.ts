import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ErrorMessages } from '../../core/errors/error-messages';
import {
  ApplyStrategyRequest, ApplyStrategyResponse, StrategyApplyItem, StrategyApplyPreview,
} from '../../core/api/models/strategies';

/** Czy akcja da się zastosować w tej chwili — tylko te dwa statusy zmieniają coś w budżecie. */
const applicable = (item: StrategyApplyItem): boolean => item.status === 'New' || item.status === 'Change';

/**
 * Okno „Zastosuj strategię w budżecie” (makieta Figma „Strategia”: 392:451) — lista akcji, które zakładają w budżecie
 * prawdziwe obiekty (cel, rezerwację, limit, zlecenie epizodyczne, zakończenie zlecenia stałego), każda ze statusem:
 * nowa, zmiana, już jest, czeka albo do uzupełnienia. Zaznaczalne są tylko „nowa” i „zmiana”.
 *
 * Okno pokazuje STAN SERWERA, nie tablicy: akcje liczy zapisana strategia, więc rodzic nie otwiera go z niezapisanymi
 * zmianami. Zastosowanie jest jedną atomową operacją (`POST /api/strategies/{id}/apply`).
 */
@Component({
  selector: 'app-strategy-apply',
  imports: [NzAlertModule, NzModalModule, NzSpinModule, TranslatePipe],
  templateUrl: './strategy-apply.html',
  styleUrl: './strategy-apply.scss',
})
export class StrategyApply {
  private readonly http = inject(HttpClient);
  private readonly message = inject(NzMessageService);
  private readonly translate = inject(TranslateService);
  private readonly errorMessages = inject(ErrorMessages);

  readonly strategyId = input.required<string>();
  /** Wariant, który stosujemy (`null` = bazowy) — wyłączone w nim akcje nie trafiają na listę. */
  readonly variantId = input<string | null>(null);
  readonly variantName = input<string | null>(null);
  readonly open = input(false);

  readonly closed = output<void>();

  protected readonly preview = signal<StrategyApplyPreview | null>(null);
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly loading = signal(false);
  protected readonly failed = signal<string | null>(null);
  protected readonly busy = signal(false);

  protected readonly count = computed(() => this.selected().size);

  /** Liczba w alercie odmieniona regułami języka (1 rzecz / 2 rzeczy / 5 rzeczy). */
  protected readonly alertKey = computed(() => {
    const lang = this.translate.currentLang() || 'pl';
    return `strategies.apply.alert.${new Intl.PluralRules(lang).select(this.count())}`;
  });

  /** Przy każdym otwarciu pobiera podgląd od nowa — stan budżetu mógł się zmienić od ostatniego razu. */
  private readonly loadOnOpen = effect(() => {
    if (!this.open()) return;
    untracked(() => void this.load());
  });

  protected locale(): string {
    return this.translate.currentLang() || 'pl';
  }

  protected isApplicable(item: StrategyApplyItem): boolean {
    return applicable(item);
  }

  protected isSelected(item: StrategyApplyItem): boolean {
    return this.selected().has(item.nodeId);
  }

  protected toggle(item: StrategyApplyItem): void {
    if (!applicable(item)) return;
    this.selected.update((set) => {
      const next = new Set(set);
      if (!next.delete(item.nodeId)) next.add(item.nodeId);
      return next;
    });
  }

  protected title(item: StrategyApplyItem): string {
    return item.title.trim() || this.translate.instant(`strategies.board.types.${item.type}`);
  }

  protected amount(value: number | null): string {
    return value === null ? '—' : new Intl.NumberFormat(this.locale(), { maximumFractionDigits: 2 }).format(value);
  }

  /** Opis pod pozycją: dla „czeka”, „już jest” i „do uzupełnienia” wynika ze statusu, w pozostałych z rodzaju akcji. */
  protected note(item: StrategyApplyItem): string {
    const key = applicable(item) ? `strategies.apply.note.${item.type}.${item.status}` : `strategies.apply.noteStatus.${item.status}`;
    return this.translate.instant(key, { current: this.amount(item.currentAmount), month: this.monthLabel(item.month) });
  }

  private monthLabel(month: string | null): string {
    if (!month) return '';
    const [year, m] = month.split('-').map(Number);
    return new Intl.DateTimeFormat(this.locale(), { month: 'long', year: 'numeric' }).format(new Date(year, m - 1, 1));
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.failed.set(null);
    try {
      const variant = this.variantId();
      const query = variant ? `?variantId=${encodeURIComponent(variant)}` : '';
      const preview = await firstValueFrom(this.http.get<StrategyApplyPreview>(`/api/strategies/${this.strategyId()}/apply${query}`));
      this.preview.set(preview);
      this.selected.set(new Set(preview.items.filter(applicable).map((i) => i.nodeId)));
    } catch (err) {
      this.preview.set(null);
      this.failed.set(this.errorMessages.of(err, 'strategies.apply.loadFailed'));
    } finally {
      this.loading.set(false);
    }
  }

  protected async apply(): Promise<void> {
    if (this.busy() || this.count() === 0) return;
    this.busy.set(true);
    try {
      const variant = this.variantId();
      const body: ApplyStrategyRequest = { nodeIds: [...this.selected()], ...(variant ? { variantId: variant } : {}) };
      const result = await firstValueFrom(
        this.http.post<ApplyStrategyResponse>(`/api/strategies/${this.strategyId()}/apply`, body),
      );
      this.message.success(this.translate.instant('strategies.apply.done', { count: result.applied.length }));
      this.closed.emit();
    } catch (err) {
      this.message.error(this.errorMessages.of(err, 'strategies.apply.failed'));
    } finally {
      this.busy.set(false);
    }
  }
}
