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
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSegmentedModule } from 'ng-zorro-antd/segmented';
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
import { CategoryOption } from '../../core/api/models/category-option';
import {
  EpisodicOrderCandidate, EpisodicOrderRow, EpisodicOrdersResponse,
} from '../../core/api/models/episodic-orders';

/** Zakładka tabeli — jedzie w adresie (`?tab=realized`), żeby lista transakcji i ekran celów mogły do niej odesłać. */
type Tab = 'planned' | 'realized';

/** Wariant modala dodawania — zaplanowane mają plan, zrealizowane wskazują transakcję. */
type Kind = 'planned' | 'realized';

/** Opóźnienie szukajki kandydatów — żeby nie pytać serwera przy każdej literze. */
const SEARCH_DEBOUNCE_MS = 300;

/** Ile zrealizowanych pokazuje karta „Zrealizowane ostatnio”. */
const RECENT_COUNT = 5;

/**
 * Ekran „Zlecenia epizodyczne” (makiety Figma, strona „Zlecenia”: 219:203, 232:938, modale 221:754 i 222:3015,
 * dialog 232:891, zgłoszenie #18).
 *
 * Większe, nieregularne wydatki: zaplanowane (lista rzeczy do kupienia, z których da się założyć cel oszczędzania)
 * i zrealizowane (powiązane z transakcją). Zrealizowane zastępują flagę „duży wydatek” — na nich stoi dowód na
 * ekranie celów.
 *
 * ⚠️ Zrealizowane NIE ma własnej kwoty, daty ani kategorii — wszystko z transakcji, więc modal „Już zrealizowane”
 * wybiera transakcję zamiast wpisywać liczby drugi raz.
 */
@Component({
  selector: 'app-episodic-orders',
  imports: [
    CommonModule, FormsModule, RouterLink, BudgetSwitcher,
    NzAlertModule, NzBreadCrumbModule, NzButtonModule, NzDatePickerModule, NzDropdownModule, NzEmptyModule,
    NzIconModule, NzInputModule, NzInputNumberModule, NzModalModule, NzProgressModule, NzSegmentedModule,
    NzSelectModule, NzSpinModule, NzStatisticModule, NzTableModule,
    TranslatePipe,
  ],
  templateUrl: './episodic-orders.html',
  styleUrl: './episodic-orders.scss',
})
export class EpisodicOrders {
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

  protected readonly tab = computed<Tab>(() => (this.queryParams().get('tab') === 'realized' ? 'realized' : 'planned'));

  /** JEDEN budżet: z adresu, a gdy adres milczy — ten z widoku (`ActiveBudget`, issue #16). */
  protected readonly budgetId = computed(() => {
    const fromUrl = this.budgetIdFromUrl();
    return this.activeBudget.resolve(fromUrl ? [fromUrl] : [])[0] ?? null;
  });

  private readonly publishBudgetFromUrl = effect(() => {
    const fromUrl = this.budgetIdFromUrl();
    if (fromUrl) this.activeBudget.set([fromUrl]);
  });

  private readonly resource = httpResource<EpisodicOrdersResponse>(() => ({
    url: '/api/episodic-orders',
    params: { ...(this.budgetId() ? { budgetId: this.budgetId()! } : {}) },
  }));

  private readonly categoriesResource = httpResource<CategoryOption[]>(() => '/api/categories');

  /** `value()` RZUCA w stanie błędu — patrz core/api/resource-value.ts. */
  private readonly value = valueOf(this.resource);
  private readonly categoriesValue = valueOf(this.categoriesResource);

  protected readonly loading = this.resource.isLoading;
  protected readonly failure = errorOf(this.resource);
  protected readonly busy = signal(false);

  protected readonly data = computed(() => this.value() ?? null);
  protected readonly categories = computed(() => this.categoriesValue() ?? []);

  /** Ostatnia odpowiedź — dla przełącznika budżetu, żeby nie znikał w trakcie przeładowania. */
  protected readonly lastData = linkedSignal<EpisodicOrdersResponse | null, EpisodicOrdersResponse | null>({
    source: this.data,
    computation: (next, prev) => next ?? prev?.value ?? null,
  });

  protected readonly rows = computed(() => (this.tab() === 'realized' ? this.data()?.realized : this.data()?.planned) ?? []);
  protected readonly recent = computed(() => (this.data()?.realized ?? []).slice(0, RECENT_COUNT));

  // ── Nawigacja ────────────────────────────────────────────────────────────────────────

  /** Wybór z przełącznika idzie do ADRESU — patrz `BudgetSwitcher`, dlaczego nie wprost do `ActiveBudget`. */
  protected switchBudget(id: string): void {
    this.navigate({ budgetId: id });
  }

  protected switchTab(tab: Tab): void {
    this.navigate({ tab: tab === 'planned' ? null : tab });
  }

  /** „Przejdź do transakcji” — lista zawężona do dnia i tytułu transakcji; filtra po identyfikatorze lista nie ma. */
  protected goToTransaction(row: EpisodicOrderRow): void {
    if (!row.date || !row.transactionDescription) return;
    const budget = this.data()?.selectedBudgetIds[0];
    void this.router.navigate(['/transactions'], {
      queryParams: {
        from: row.date, to: row.date, search: row.transactionDescription,
        ...(budget ? { budgetId: budget } : {}),
      },
    });
  }

  private navigate(queryParams: Record<string, string | null>): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge' });
  }

  // ── Modal zlecenia (makiety 221:754, 222:3015) ───────────────────────────────────────

  protected readonly editorOpen = signal(false);
  /** Zlecenie w edycji; `null` = nowe. */
  protected readonly editing = signal<EpisodicOrderRow | null>(null);
  protected readonly kind = signal<Kind>('planned');
  protected readonly draftName = signal('');
  protected readonly draftDescription = signal('');
  protected readonly draftCategoryId = signal<string | null>(null);
  protected readonly draftAmount = signal<number | null>(null);
  protected readonly draftDueMonth = signal<Date | null>(null);
  protected readonly draftTransactionId = signal<string | null>(null);

  /** Zmiana zrealizowanego to tylko nazwa i opis — liczby niesie transakcja. */
  protected readonly editingRealized = computed(() => this.editing()?.transactionId != null);

  protected readonly canSave = computed(() => {
    if (this.draftName().trim().length === 0) return false;
    if (this.editingRealized()) return true;
    if (this.editing() === null && this.kind() === 'realized') return this.draftTransactionId() !== null;
    return this.draftCategoryId() !== null && (this.draftAmount() ?? 0) > 0 && this.draftDueMonth() !== null;
  });

  protected openEditor(row: EpisodicOrderRow | null): void {
    this.editing.set(row);
    this.kind.set(row?.transactionId ? 'realized' : 'planned');
    this.draftName.set(row?.name ?? '');
    this.draftDescription.set(row?.description ?? '');
    this.draftCategoryId.set(row?.categoryId ?? null);
    this.draftAmount.set(row && !row.transactionId ? row.amount : null);
    this.draftDueMonth.set(row?.dueMonth ? this.dateOf(row.dueMonth) : null);
    this.draftTransactionId.set(null);
    this.candidates.set([]);
    this.editorOpen.set(true);
  }

  protected switchKind(kind: Kind): void {
    this.kind.set(kind);
    if (kind === 'realized' && this.candidates().length === 0) this.searchCandidates('');
  }

  protected async save(): Promise<void> {
    if (!this.canSave()) return;

    const existing = this.editing();
    const realizedNew = existing === null && this.kind() === 'realized';
    const due = this.draftDueMonth();
    const body = {
      budgetId: this.data()?.selectedBudgetIds[0] ?? this.budgetId(),
      name: this.draftName().trim(),
      description: this.draftDescription().trim() || null,
      transactionId: realizedNew ? this.draftTransactionId() : null,
      categoryId: realizedNew ? null : this.draftCategoryId(),
      amount: realizedNew ? null : this.draftAmount(),
      dueMonth: realizedNew || !due ? null : this.isoMonth(due),
    };

    await this.run(async () => {
      await firstValueFrom(existing
        ? this.http.put(`/api/episodic-orders/${existing.id}`, body)
        : this.http.post('/api/episodic-orders', body));
      this.editorOpen.set(false);
    }, 'episodicOrders.saved');
  }

  // ── Kandydaci: modal „Już zrealizowane” i dialog „Oznacz jako kupione” ─────────────────

  protected readonly candidates = signal<EpisodicOrderCandidate[]>([]);
  protected readonly candidatesLoading = signal(false);
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  /** Kandydaci liczą się na SERWERZE — ten sam warunek, który sprawdzi zapis. */
  protected searchCandidates(search: string, orderId: string | null = null): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => void this.loadCandidates(search, orderId), SEARCH_DEBOUNCE_MS);
  }

  private async loadCandidates(search: string, orderId: string | null): Promise<void> {
    const budget = this.data()?.selectedBudgetIds[0] ?? this.budgetId();
    const params: Record<string, string> = {
      ...(orderId ? { orderId } : budget ? { budgetId: budget } : {}),
      ...(search.trim() ? { search: search.trim() } : {}),
    };
    this.candidatesLoading.set(true);
    try {
      this.candidates.set(await firstValueFrom(
        this.http.get<EpisodicOrderCandidate[]>('/api/episodic-orders/candidates', { params })));
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.candidatesLoading.set(false);
    }
  }

  protected readonly chosenCandidate = computed(() =>
    this.candidates().find((c) => c.id === this.draftTransactionId()) ?? null);

  // ── „Oznacz jako kupione…” (makieta 232:891) ─────────────────────────────────────────

  protected readonly purchaseOpen = signal(false);
  protected readonly purchasing = signal<EpisodicOrderRow | null>(null);
  protected readonly purchaseSearch = signal('');
  protected readonly purchaseTransactionId = signal<string | null>(null);

  protected openPurchase(row: EpisodicOrderRow): void {
    this.purchasing.set(row);
    this.purchaseSearch.set('');
    this.purchaseTransactionId.set(null);
    this.candidates.set([]);
    this.purchaseOpen.set(true);
    void this.loadCandidates('', row.id);
  }

  protected onPurchaseSearch(value: string): void {
    this.purchaseSearch.set(value);
    const row = this.purchasing();
    if (row) this.searchCandidates(value, row.id);
  }

  protected async confirmPurchase(): Promise<void> {
    const row = this.purchasing();
    const transactionId = this.purchaseTransactionId();
    if (!row || !transactionId) return;

    await this.run(async () => {
      await firstValueFrom(this.http.put(`/api/episodic-orders/${row.id}/purchase`, { transactionId }));
      this.purchaseOpen.set(false);
    }, 'episodicOrders.purchased');
  }

  // ── Akcje wiersza ────────────────────────────────────────────────────────────────────

  /** „Załóż cel oszczędzania” — bez dialogu: rezerwacja bierze nazwę, kwotę i termin ze zlecenia. */
  protected async createReservation(row: EpisodicOrderRow): Promise<void> {
    await this.run(
      () => firstValueFrom(this.http.post(`/api/episodic-orders/${row.id}/reservation`, {})),
      'episodicOrders.reservationCreated', { name: row.name });
  }

  protected async undoPurchase(row: EpisodicOrderRow): Promise<void> {
    await this.run(
      () => firstValueFrom(this.http.delete(`/api/episodic-orders/${row.id}/purchase`)),
      'episodicOrders.undone');
  }

  protected async remove(row: EpisodicOrderRow): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('episodicOrders.removeConfirm.header', { name: row.name }),
      description: this.translate.instant(row.transactionId
        ? 'episodicOrders.removeConfirm.realized'
        : row.reservationId ? 'episodicOrders.removeConfirm.withReservation' : 'episodicOrders.removeConfirm.planned'),
      confirmText: this.translate.instant('episodicOrders.remove'),
      danger: true,
    });
    if (!ok) return;

    await this.run(() => firstValueFrom(this.http.delete(`/api/episodic-orders/${row.id}`)), 'episodicOrders.removed');
  }

  private async run(action: () => Promise<unknown>, successKey: string, params?: object): Promise<void> {
    this.busy.set(true);
    try {
      await action();
      this.message.success(this.translate.instant(successKey, params));
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

  /** Kwota bez groszy — kafle i tabela, jak na makiecie („4 000 zł”). */
  protected wholeMoney(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat('pl-PL', { maximumFractionDigits: 0 }).format(value);
  }

  /** „03.2027” — termin zaplanowanego. */
  protected monthYear(iso: string | null): string {
    if (!iso) return '—';
    const [year, month] = iso.split('-');
    return `${month}.${year}`;
  }

  /** „3.09.2026” — data zrealizowanego. */
  protected fullDate(iso: string | null): string {
    if (!iso) return '—';
    const [year, month, day] = iso.split('-').map(Number);
    return `${day}.${String(month).padStart(2, '0')}.${year}`;
  }

  /** „3.09” — karta ostatnio zrealizowanych. */
  protected dayMonth(iso: string | null): string {
    if (!iso) return '';
    const [, month, day] = iso.split('-').map(Number);
    return `${day}.${String(month).padStart(2, '0')}`;
  }

  /** Procent uzbieranego do paska — przy braku rezerwacji paska nie ma. */
  protected collectedPercent(row: EpisodicOrderRow): number {
    return row.amount > 0 ? Math.min(100, Math.round(((row.collected ?? 0) / row.amount) * 100)) : 0;
  }

  /** Polska liczba mnoga: 1 „zakup”, 2–4 „zakupy”, 5+ i 12–14 „zakupów”. */
  protected pluralForm(count: number): 'one' | 'few' | 'many' {
    if (count === 1) return 'one';
    const tens = count % 100;
    const units = count % 10;
    return units >= 2 && units <= 4 && (tens < 12 || tens > 14) ? 'few' : 'many';
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
