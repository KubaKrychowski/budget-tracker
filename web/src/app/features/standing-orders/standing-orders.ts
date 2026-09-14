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
import { NzDropdownModule } from 'ng-zorro-antd/dropdown';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
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
import {
  StandingOrderMonthState, StandingOrderPin, StandingOrderPreview, StandingOrderRhythm, StandingOrderRow,
  StandingOrdersResponse,
} from '../../core/api/models/standing-orders';

/** Najkrótsza fraza reguły — ta sama co `StandingOrderRuleValidator.MinPatternLength` w API. */
const MIN_PATTERN_LENGTH = 3;

/** Opóźnienie podglądu reguły — żeby nie pytać serwera przy każdej literze. */
const PREVIEW_DEBOUNCE_MS = 300;

/**
 * Ekran „Zlecenia stałe” (makieta Figma, strona „Zlecenia”: 219:3, modal 221:296, zgłoszenie #18).
 *
 * Zlecenie to nazwane zobowiązanie („Czynsz”) z regułą: tytuł zawiera + zakres kwoty. Reguła przypina transakcje
 * WSTECZ i przy imporcie. ⚠️ Przypięcie NIE zmienia kategorii (decyzja użytkownika) — dlatego kolumna „Kategoria”
 * pokazuje kategorię przypiętych transakcji, a nie ustawienie zlecenia.
 */
@Component({
  selector: 'app-standing-orders',
  imports: [
    CommonModule, FormsModule, RouterLink, BudgetSwitcher,
    NzAlertModule, NzBreadCrumbModule, NzButtonModule, NzDropdownModule, NzEmptyModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzSpinModule, NzStatisticModule, NzTableModule,
    TranslatePipe,
  ],
  templateUrl: './standing-orders.html',
  styleUrl: './standing-orders.scss',
})
export class StandingOrders {
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
  private readonly monthFromUrl = computed(() => this.queryParams().get('month'));

  /** JEDEN budżet: z adresu, a gdy adres milczy — ten z widoku (`ActiveBudget`, issue #16). */
  protected readonly budgetId = computed(() => {
    const fromUrl = this.budgetIdFromUrl();
    return this.activeBudget.resolve(fromUrl ? [fromUrl] : [])[0] ?? null;
  });

  private readonly publishBudgetFromUrl = effect(() => {
    const fromUrl = this.budgetIdFromUrl();
    if (fromUrl) this.activeBudget.set([fromUrl]);
  });

  private readonly resource = httpResource<StandingOrdersResponse>(() => ({
    url: '/api/standing-orders',
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

  /** Ostatnia odpowiedź — dla przełącznika budżetu i paska miesięcy, żeby nie znikały w trakcie przeładowania. */
  protected readonly lastData = linkedSignal<StandingOrdersResponse | null, StandingOrdersResponse | null>({
    source: this.data,
    computation: (next, prev) => next ?? prev?.value ?? null,
  });

  protected readonly isCurrentMonth = computed(() => {
    const d = this.lastData();
    return d !== null && d.month === d.currentMonth;
  });

  /** Zlecenia, które czekają (bieżący miesiąc) — materiał na baner. */
  protected readonly waiting = computed(() => (this.data()?.orders ?? []).filter((o) => o.state === 'Waiting'));
  /** Zlecenia, które nie zeszły w zamkniętym miesiącu. */
  protected readonly missed = computed(() => (this.data()?.orders ?? []).filter((o) => o.state === 'Missed'));
  /** Zlecenia, które zeszły w innej kwocie niż zwykle. */
  protected readonly different = computed(
    () => (this.data()?.orders ?? []).filter((o) => o.state === 'PaidDifferentAmount'),
  );

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

  protected goToCurrentMonth(): void {
    this.navigate({ month: null });
  }

  /** „Przejdź do powiązanych” — lista transakcji zawężona do zlecenia, z okruszkiem powrotu tutaj. */
  protected goToLinked(row: StandingOrderRow): void {
    const budget = this.data()?.selectedBudgetIds[0];
    void this.router.navigate(['/transactions'], {
      queryParams: { standingOrderId: row.id, origin: 'standing-orders', ...(budget ? { budgetId: budget } : {}) },
    });
  }

  private navigate(queryParams: Record<string, string | null>): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge' });
  }

  // ── Modal zlecenia ───────────────────────────────────────────────────────────────────

  protected readonly rhythms: StandingOrderRhythm[] = ['Monthly', 'Quarterly', 'Yearly'];
  protected readonly months = Array.from({ length: 12 }, (_, i) => i + 1);

  protected readonly editorOpen = signal(false);
  /** Zlecenie w edycji; `null` = nowe. */
  protected readonly editing = signal<StandingOrderRow | null>(null);
  protected readonly draftName = signal('');
  protected readonly draftAmount = signal<number | null>(null);
  protected readonly draftRhythm = signal<StandingOrderRhythm>('Monthly');
  protected readonly draftDueMonth = signal<number | null>(null);
  protected readonly draftPattern = signal('');
  protected readonly draftFrom = signal<number | null>(null);
  protected readonly draftTo = signal<number | null>(null);

  protected readonly preview = signal<StandingOrderPreview | null>(null);
  private previewTimer: ReturnType<typeof setTimeout> | undefined;

  private readonly rangeValid = computed(() => {
    const from = this.draftFrom();
    const to = this.draftTo();
    return from !== null && to !== null && from >= 0 && to > 0 && from <= to;
  });

  protected readonly canSave = computed(() =>
    this.draftName().trim().length > 0
    && (this.draftAmount() ?? 0) > 0
    && this.draftPattern().trim().length >= MIN_PATTERN_LENGTH
    && this.rangeValid()
    && (this.draftRhythm() === 'Monthly' || this.draftDueMonth() !== null));

  /**
   * Podgląd reguły liczy SERWER, na całej historii budżetu — ten sam zbiór, który przypnie zapis.
   * Zliczanie po stronie przeglądarki na jednej stronie listy pokazywałoby liczbę, która nie ma nic wspólnego z zapisem.
   */
  private readonly schedulePreview = effect(() => {
    const open = this.editorOpen();
    const pattern = this.draftPattern().trim();
    const from = this.draftFrom();
    const to = this.draftTo();
    const valid = this.rangeValid();
    const editingId = this.editing()?.id ?? null;

    clearTimeout(this.previewTimer);
    if (!open || pattern.length < MIN_PATTERN_LENGTH || !valid) {
      this.preview.set(null);
      return;
    }

    const body = {
      budgetId: this.data()?.selectedBudgetIds[0] ?? this.budgetId(),
      standingOrderId: editingId,
      titlePattern: pattern,
      amountFrom: from,
      amountTo: to,
    };
    this.previewTimer = setTimeout(() => void this.loadPreview(body), PREVIEW_DEBOUNCE_MS);
  });

  private async loadPreview(body: object): Promise<void> {
    try {
      this.preview.set(await firstValueFrom(this.http.post<StandingOrderPreview>('/api/standing-orders/preview', body)));
    } catch {
      this.preview.set(null);
    }
  }

  protected openEditor(row: StandingOrderRow | null): void {
    this.editing.set(row);
    this.draftName.set(row?.name ?? '');
    this.draftAmount.set(row?.expectedAmount ?? null);
    this.draftRhythm.set(row?.rhythm ?? 'Monthly');
    this.draftDueMonth.set(row?.dueMonth ?? null);
    this.draftPattern.set(row?.titlePattern ?? '');
    this.draftFrom.set(row?.amountFrom ?? null);
    this.draftTo.set(row?.amountTo ?? null);
    this.preview.set(null);
    this.editorOpen.set(true);
  }

  protected async save(): Promise<void> {
    if (!this.canSave()) return;

    const body = {
      budgetId: this.data()?.selectedBudgetIds[0] ?? this.budgetId(),
      name: this.draftName().trim(),
      expectedAmount: this.draftAmount(),
      rhythm: this.draftRhythm(),
      dueMonth: this.draftRhythm() === 'Monthly' ? null : this.draftDueMonth(),
      titlePattern: this.draftPattern().trim(),
      amountFrom: this.draftFrom(),
      amountTo: this.draftTo(),
    };
    const existing = this.editing();

    this.busy.set(true);
    try {
      const saved = await firstValueFrom(existing
        ? this.http.put<{ linkedCount: number }>(`/api/standing-orders/${existing.id}`, body)
        : this.http.post<{ linkedCount: number }>('/api/standing-orders', body));
      this.editorOpen.set(false);
      this.message.success(this.translate.instant('standingOrders.saved', { count: saved.linkedCount }));
      this.resource.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(row: StandingOrderRow): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('standingOrders.removeConfirm.header', { name: row.name }),
      description: this.translate.instant('standingOrders.removeConfirm.description', { count: row.linkedCount }),
      confirmText: this.translate.instant('standingOrders.remove'),
      danger: true,
    });
    if (!ok) return;

    await this.run(() => firstValueFrom(this.http.delete(`/api/standing-orders/${row.id}`)), 'standingOrders.removed');
  }

  protected async unpin(pin: StandingOrderPin): Promise<void> {
    await this.run(
      () => firstValueFrom(this.http.delete(`/api/standing-orders/pins/${pin.transactionId}`)),
      'standingOrders.unpinned');
  }

  private async run(action: () => Promise<unknown>, successKey: string): Promise<void> {
    this.busy.set(true);
    try {
      await action();
      this.message.success(this.translate.instant(successKey));
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

  /** Nazwa miesiąca w mianowniku dla wyboru miesiąca zlecenia rocznego/kwartalnego. */
  protected monthName(month: number): string {
    return new Intl.DateTimeFormat('pl-PL', { month: 'long' }).format(new Date(2026, month - 1, 1));
  }

  /** „5.09” — dzień i miesiąc, bez roku, jak na makiecie. */
  protected dayMonth(iso: string | null | undefined): string {
    if (!iso) return '';
    const [, month, day] = iso.split('-').map(Number);
    return `${day}.${String(month).padStart(2, '0')}`;
  }

  /** Kolor kropki stanu — znaczenie niesie kolor, jak w statusach listy transakcji. */
  protected stateClass(state: StandingOrderMonthState): string {
    return `so__dot--${state.charAt(0).toLowerCase()}${state.slice(1)}`;
  }

  /** Lista „Kredyt (653,00 zł), Internet (60,00 zł)” do banera. */
  protected nameList(rows: readonly StandingOrderRow[], amount: 'expected' | 'paid' = 'expected'): string {
    return rows
      .map((r) => `${r.name} (${this.money(amount === 'paid' ? r.paidAmount : r.expectedAmount)} zł)`)
      .join(', ');
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
