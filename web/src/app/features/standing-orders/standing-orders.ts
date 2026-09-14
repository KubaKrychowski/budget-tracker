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
  StandingOrderRule, StandingOrdersResponse,
} from '../../core/api/models/standing-orders';

/** Najkrótsza fraza reguły — ta sama co `StandingOrderRuleValidator.MinPatternLength` w API. */
const MIN_PATTERN_LENGTH = 3;

/** Najwięcej reguł na zlecenie — ta sama co `StandingOrderRuleValidator.MaxRules` w API. */
const MAX_RULES = 20;

/** Opóźnienie podglądu reguły — żeby nie pytać serwera przy każdej literze. */
const PREVIEW_DEBOUNCE_MS = 300;

const ROMAN_MONTHS = ['I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI', 'XII'];

/** Reguła w trakcie edycji — kwoty mogą być puste, dopóki użytkownik ich nie wpisze. */
interface DraftRule {
  readonly pattern: string;
  readonly from: number | null;
  readonly to: number | null;
}

const emptyRule = (): DraftRule => ({ pattern: '', from: null, to: null });

const ruleValid = (r: DraftRule): boolean =>
  r.pattern.trim().length >= MIN_PATTERN_LENGTH
  && r.from !== null && r.to !== null && r.from >= 0 && r.to > 0 && r.from <= r.to;

const rulesOf = (rules: readonly DraftRule[]): StandingOrderRule[] =>
  rules.map((r) => ({ titlePattern: r.pattern.trim(), amountFrom: r.from!, amountTo: r.to! }));

/** Polska liczba mnoga: 2–4 „reguły”, 5+ i 12–14 „reguł”. */
const pluralForm = (count: number): 'few' | 'many' => {
  const tens = count % 100;
  const units = count % 10;
  return units >= 2 && units <= 4 && (tens < 12 || tens > 14) ? 'few' : 'many';
};

/**
 * Ekran „Zlecenia stałe” (makieta Figma, strona „Zlecenia”: 219:3, modal 221:296, zgłoszenie #18).
 *
 * Zlecenie to nazwane zobowiązanie („Czynsz”) z regułami: tytuł zawiera + zakres kwoty, łączonymi „lub”. Reguły
 * przypinają transakcje WSTECZ i przy imporcie. Zakończone zlecenie (dialog 229:1110) przestaje być oczekiwane
 * po ostatnim miesiącu, ale zostaje z historią. ⚠️ Przypięcie NIE zmienia kategorii (decyzja użytkownika) — dlatego kolumna „Kategoria”
 * pokazuje kategorię przypiętych transakcji, a nie ustawienie zlecenia.
 */
@Component({
  selector: 'app-standing-orders',
  imports: [
    CommonModule, FormsModule, RouterLink, BudgetSwitcher,
    NzAlertModule, NzBreadCrumbModule, NzButtonModule, NzDatePickerModule, NzDropdownModule, NzEmptyModule, NzIconModule, NzInputModule,
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

  /** Zlecenia trwające w oglądanym miesiącu — główna część tabeli. */
  protected readonly activeOrders = computed(() => (this.data()?.orders ?? []).filter((o) => o.state !== 'Ended'));
  /**
   * Zakończone PRZED oglądanym miesiącem — zwijana sekcja pod „Razem miesięcznie” (makieta 219:3).
   * Do ostatniego miesiąca włącznie zlecenie jest zwykłym wierszem, więc cofnięcie się strzałkami pokazuje, czy wtedy zeszło.
   */
  protected readonly endedOrders = computed(() => (this.data()?.orders ?? []).filter((o) => o.state === 'Ended'));
  protected readonly endedOpen = signal(false);

  /** Zlecenia, które czekają (bieżący miesiąc) — materiał na baner. */
  protected readonly waiting = computed(() => this.activeOrders().filter((o) => o.state === 'Waiting'));
  /** Zlecenia, które nie zeszły w zamkniętym miesiącu. */
  protected readonly missed = computed(() => this.activeOrders().filter((o) => o.state === 'Missed'));
  /** Zlecenia, które zeszły w innej kwocie niż zwykle. */
  protected readonly different = computed(() => this.activeOrders().filter((o) => o.state === 'PaidDifferentAmount'));

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
  /** Reguły dopasowania w kolejności z modala; zlecenie ma co najmniej jedną. */
  protected readonly draftRules = signal<DraftRule[]>([emptyRule()]);

  protected readonly preview = signal<StandingOrderPreview | null>(null);
  private previewTimer: ReturnType<typeof setTimeout> | undefined;

  private readonly rulesValid = computed(() => {
    const rules = this.draftRules();
    return rules.length > 0 && rules.length <= MAX_RULES && rules.every(ruleValid);
  });

  protected readonly canAddRule = computed(() => this.draftRules().length < MAX_RULES);

  protected readonly canSave = computed(() =>
    this.draftName().trim().length > 0
    && (this.draftAmount() ?? 0) > 0
    && this.rulesValid()
    && (this.draftRhythm() === 'Monthly' || this.draftDueMonth() !== null));

  protected addRule(): void {
    if (this.canAddRule()) this.draftRules.update((rules) => [...rules, emptyRule()]);
  }

  /** Ostatniej reguły nie da się usunąć — zlecenie bez reguły nie przypnie niczego. */
  protected removeRule(index: number): void {
    this.draftRules.update((rules) => (rules.length > 1 ? rules.filter((_, i) => i !== index) : rules));
  }

  protected updateRule(index: number, patch: Partial<DraftRule>): void {
    this.draftRules.update((rules) => rules.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  /**
   * Podgląd reguł liczy SERWER, na całej historii budżetu — ten sam zbiór, który przypnie zapis.
   * Zliczanie po stronie przeglądarki na jednej stronie listy pokazywałoby liczbę, która nie ma nic wspólnego z zapisem.
   * Pyta dopiero, gdy WSZYSTKIE reguły są poprawne — liczba dla części reguł myliłaby tak samo.
   */
  private readonly schedulePreview = effect(() => {
    const open = this.editorOpen();
    const rules = this.draftRules();
    const valid = this.rulesValid();
    const editingId = this.editing()?.id ?? null;

    clearTimeout(this.previewTimer);
    if (!open || !valid) {
      this.preview.set(null);
      return;
    }

    const body = {
      budgetId: this.data()?.selectedBudgetIds[0] ?? this.budgetId(),
      standingOrderId: editingId,
      rules: rulesOf(rules),
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
    this.draftRules.set(row && row.rules.length > 0
      ? row.rules.map((r) => ({ pattern: r.titlePattern, from: r.amountFrom, to: r.amountTo }))
      : [emptyRule()]);
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
      rules: rulesOf(this.draftRules()),
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

  // ── Zakończenie (makieta 229:1110) ───────────────────────────────────────────────────

  protected readonly endOpen = signal(false);
  protected readonly endTarget = signal<StandingOrderRow | null>(null);
  /** Ostatni miesiąc zlecenia — domyślnie bieżący, jak na makiecie. */
  protected readonly endLastMonth = signal<Date | null>(null);

  protected openEnd(row: StandingOrderRow): void {
    const current = this.data()?.currentMonth;
    this.endTarget.set(row);
    this.endLastMonth.set(current ? this.dateOf(current) : new Date());
    this.endOpen.set(true);
  }

  protected async confirmEnd(): Promise<void> {
    const row = this.endTarget();
    const lastMonth = this.endLastMonth();
    if (!row || !lastMonth) return;

    this.endOpen.set(false);
    await this.run(
      () => firstValueFrom(this.http.put(`/api/standing-orders/${row.id}/end`, { lastMonth: this.isoMonth(lastMonth) })),
      'standingOrders.ended');
  }

  /** „Wznów” zdejmuje datę końca — bez dialogu, bo nic nie znika i da się to od razu cofnąć „Zakończ”. */
  protected async resume(row: StandingOrderRow): Promise<void> {
    await this.run(() => firstValueFrom(this.http.delete(`/api/standing-orders/${row.id}/end`)), 'standingOrders.resumed');
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

  /** „VIII 2026” — miesiąc zakończenia rzymską cyfrą, jak na makiecie. */
  protected romanMonth(iso: string | null | undefined): string {
    if (!iso) return '';
    const [year, month] = iso.split('-').map(Number);
    return `${ROMAN_MONTHS[month - 1]} ${year}`;
  }

  /**
   * Opis reguł pod nazwą. Przy jednej regule pełny („tytuł zawiera „czynsz” · 2000–2500 zł”), przy kilku skrót
   * z samymi frazami — pełny opis z kwotami się nie mieścił (makieta 219:3).
   */
  protected rulesSummary(row: StandingOrderRow): string {
    const [first] = row.rules;
    if (!first) return '';
    if (row.rules.length === 1) {
      return this.translate.instant('standingOrders.table.rule', {
        pattern: first.titlePattern, from: this.money(first.amountFrom), to: this.money(first.amountTo),
      });
    }
    return this.translate.instant(`standingOrders.table.rules.${pluralForm(row.rules.length)}`, {
      count: row.rules.length,
      list: row.rules.map((r) => `„${r.titlePattern}”`).join(', '),
    });
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
