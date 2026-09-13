import { Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTableModule } from 'ng-zorro-antd/table';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ActiveBudget } from '../../core/active-budget';
import { BudgetSwitcher } from '../../core/budget-switcher/budget-switcher';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ErrorMessages } from '../../core/errors/error-messages';
import { errorOf, valueOf } from '../../core/api/resource-value';
import { parseAmount } from '../../core/parse-amount';
import { LimitRow, LimitsResponse } from '../../core/api/models/limits';

/** Próg ostrzeżenia nowego limitu — ten sam co domyślny po stronie serwera (`LimitWarning.DefaultThreshold`). */
const DEFAULT_WARNING_THRESHOLD = 80;

/**
 * Ekran „Limity wydatków" (makieta Figma, strona „Limity wydatków", ramki 203:3, 203:13079, 206:182).
 *
 * Model (decyzja użytkownika): limit na KATEGORIĘ, a miesięczny limit budżetu to ich suma. Bez podpowiedzi
 * kwot — prognozy są osobnym zadaniem. Limit ma historię („obowiązuje od"), a próg ostrzeżenia jest
 * ustawiany przy każdym limicie osobno.
 */
@Component({
  selector: 'app-limits',
  imports: [
    CommonModule, FormsModule, RouterLink, BudgetSwitcher,
    NzAlertModule, NzBreadCrumbModule, NzButtonModule, NzDatePickerModule, NzEmptyModule, NzIconModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzSpinModule, NzStatisticModule, NzTableModule,
    TranslatePipe,
  ],
  templateUrl: './limits.html',
  styleUrl: './limits.scss',
})
export class Limits {
  /** Parser polskiego formatu kwot dla pól `nz-input-number` — uzasadnienie przy `parseAmount`. */
  protected readonly parseAmount = parseAmount;

  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly errorMessages = inject(ErrorMessages);
  private readonly activeBudget = inject(ActiveBudget);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  private readonly budgetIdFromUrl = computed(() => this.queryParams().get('budgetId'));

  /** Oglądany miesiąc z adresu (`?month=2026-08-01`); `null` = bieżący, rozstrzyga serwer. */
  private readonly monthFromUrl = computed(() => this.queryParams().get('month'));

  /**
   * JEDEN budżet: z adresu, a gdy adres milczy — ten z widoku (`ActiveBudget`, issue #16).
   *
   * ⚠️ Pojedynczy, w odróżnieniu od listy transakcji: limity są zasadą jednego budżetu, a suma limitów
   * dwóch wariantów tych samych danych nie znaczy nic.
   */
  protected readonly budgetId = computed(() => {
    const fromUrl = this.budgetIdFromUrl();
    return this.activeBudget.resolve(fromUrl ? [fromUrl] : [])[0] ?? null;
  });

  private readonly publishBudgetFromUrl = effect(() => {
    const fromUrl = this.budgetIdFromUrl();
    if (fromUrl) this.activeBudget.set([fromUrl]);
  });

  private readonly resource = httpResource<LimitsResponse>(() => ({
    url: '/api/limits',
    params: {
      ...(this.budgetId() ? { budgetId: this.budgetId()! } : {}),
      ...(this.monthFromUrl() ? { month: this.monthFromUrl()! } : {}),
    },
  }));

  /** `value()` RZUCA w stanie błędu — patrz core/api/resource-value.ts. */
  private readonly value = valueOf(this.resource);

  protected readonly loading = this.resource.isLoading;
  protected readonly failure = errorOf(this.resource);
  protected readonly busy = signal(false);

  protected readonly data = computed(() => this.value() ?? null);

  /** Ostatnia odpowiedź — tylko dla przełącznika budżetu i paska miesięcy, żeby nie znikały w trakcie przeładowania. */
  protected readonly lastData = linkedSignal<LimitsResponse | null, LimitsResponse | null>({
    source: this.data,
    computation: (next, prev) => next ?? prev?.value ?? null,
  });

  protected readonly rows = computed(() => this.data()?.limits ?? []);
  protected readonly readOnly = computed(() => this.data()?.readOnly ?? false);
  protected readonly isCurrentMonth = computed(() => {
    const d = this.lastData();
    return d !== null && d.month === d.currentMonth;
  });

  protected readonly overRows = computed(() => this.rows().filter((r) => r.state === 'Over'));

  // ── Nawigacja ────────────────────────────────────────────────────────────────────────

  /** Wybór z przełącznika idzie do ADRESU — patrz `BudgetSwitcher`, dlaczego nie wprost do `ActiveBudget`. */
  protected switchBudget(id: string): void {
    this.navigate({ budgetId: id });
  }

  protected shiftMonth(delta: number): void {
    const month = this.lastData()?.month;
    if (!month) return;
    const [year, m] = month.split('-').map(Number);
    this.navigate({ month: this.isoMonth(new Date(year, m - 1 + delta, 1)) });
  }

  /** „Bieżący miesiąc" zdejmuje parametr — o bieżącym miesiącu decyduje zegar serwera, nie przeglądarki. */
  protected goToCurrentMonth(): void {
    this.navigate({ month: null });
  }

  private navigate(queryParams: Record<string, string | null>): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge' });
  }

  // ── Modal „Ustaw limit" ──────────────────────────────────────────────────────────────

  protected readonly editorOpen = signal(false);
  /** Wiersz w edycji; `null` = nowy limit. */
  protected readonly editing = signal<LimitRow | null>(null);
  protected readonly draftCategoryId = signal<string | null>(null);
  protected readonly draftAmount = signal<number | null>(null);
  protected readonly draftThreshold = signal<number | null>(DEFAULT_WARNING_THRESHOLD);
  protected readonly draftValidFrom = signal<Date | null>(null);

  /** Kategorie, które da się wybrać przy DODAWANIU — bez tych, które już mają limit w oglądanym miesiącu. */
  protected readonly availableCategories = computed(
    () => (this.data()?.categories ?? []).filter((c) => !c.hasLimit),
  );

  /**
   * Miesięczny limit budżetu po zapisie — zdanie z makiety („podniesie go do 3 550 zł").
   *
   * Liczone względem OGLĄDANEGO miesiąca: to on jest na ekranie i to jego sumę użytkownik ma przed oczami.
   */
  protected readonly totalAfterSave = computed(() => {
    const total = this.data()?.limitTotal ?? 0;
    return total - (this.editing()?.limit ?? 0) + (this.draftAmount() ?? 0);
  });

  protected readonly canSave = computed(() =>
    this.draftCategoryId() !== null
    && (this.draftAmount() ?? 0) > 0
    && (this.draftThreshold() ?? 0) >= 1 && (this.draftThreshold() ?? 0) <= 100
    && this.draftValidFrom() !== null);

  /**
   * Blokada minionych miesięcy w wyborze „obowiązuje od" — tylko przy ZMIANIE istniejącego limitu.
   *
   * Pierwszy limit kategorii wolno ustawić wstecz (serwer na to pozwala, patrz `SetLimitCommandHandler`),
   * więc przy dodawaniu kalendarz nie może tego zabraniać — zabroniłby jedynej drogi do historii.
   */
  protected readonly disabledMonth = (date: Date): boolean => {
    if (this.editing() === null) return false;
    const current = this.data()?.currentMonth;
    return current !== undefined && this.isoMonth(date) < current;
  };

  protected openEditor(row: LimitRow | null, categoryId: string | null = null): void {
    const current = this.data()?.currentMonth;
    this.editing.set(row);
    this.draftCategoryId.set(row?.categoryId ?? categoryId);
    this.draftAmount.set(row?.limit ?? null);
    this.draftThreshold.set(row?.warningThreshold ?? DEFAULT_WARNING_THRESHOLD);
    this.draftValidFrom.set(current ? this.dateOf(current) : null);
    this.editorOpen.set(true);
  }

  protected async save(): Promise<void> {
    if (!this.canSave()) return;

    this.busy.set(true);
    try {
      await firstValueFrom(this.http.post('/api/limits', {
        budgetId: this.data()?.selectedBudgetIds[0] ?? this.budgetId(),
        categoryId: this.draftCategoryId(),
        amount: this.draftAmount(),
        warningThreshold: this.draftThreshold(),
        validFrom: this.isoMonth(this.draftValidFrom()!),
      }));
      this.editorOpen.set(false);
      this.message.success(this.translate.instant('limits.saved'));
      this.resource.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(row: LimitRow): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('limits.removeConfirm.header', { name: row.categoryName }),
      description: this.translate.instant('limits.removeConfirm.description', {
        limit: this.money(row.limit),
        spent: this.money(row.spent),
        total: this.money((this.data()?.limitTotal ?? 0) - row.limit),
      }),
      confirmText: this.translate.instant('limits.remove'),
    });
    if (!ok) return;

    this.busy.set(true);
    try {
      await firstValueFrom(this.http.delete(`/api/limits/${row.id}`));
      this.message.success(this.translate.instant('limits.removed'));
      this.resource.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected reload(): void {
    this.resource.reload();
  }

  protected errorText(error: unknown): string {
    return this.errorMessages.of(error);
  }

  // ── Formatowanie ─────────────────────────────────────────────────────────────────────

  protected money(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
  }

  protected monthLabel(iso: string | null | undefined): string {
    if (!iso) return '';
    const label = new Intl.DateTimeFormat('pl-PL', { month: 'long', year: 'numeric' }).format(this.dateOf(iso));
    return label.charAt(0).toUpperCase() + label.slice(1);
  }

  /**
   * Miesiąc liczbowo („09.2026") — do wtrąceń w zdaniu („od 09.2026: …").
   *
   * Nazwa miesiąca w mianowniku („od wrzesień") jest niegramatyczna, a dopełniacza `Intl` nie zna.
   */
  protected monthShort(iso: string | null | undefined): string {
    if (!iso) return '';
    const [year, month] = iso.split('-');
    return `${month}.${year}`;
  }

  /** Szerokość wypełnienia paska — przy przekroczeniu pasek jest pełny, a nie wychodzi poza tor. */
  protected fillWidth(row: LimitRow): number {
    return Math.min(row.percent, 100);
  }

  private dateOf(iso: string): Date {
    const [year, month] = iso.split('-').map(Number);
    return new Date(year, month - 1, 1);
  }

  /** Pierwszy dzień miesiąca LOKALNIE — `toISOString` przesunąłby datę o strefę czasową. */
  private isoMonth(date: Date): string {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-01`;
  }
}
