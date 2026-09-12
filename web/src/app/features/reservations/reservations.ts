import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ErrorMessages } from '../../core/errors/error-messages';
import { errorOf, valueOf } from '../../core/api/resource-value';
import { parseAmount } from '../../core/parse-amount';
import {
  ReservationsResponse, SavingsReservation, SettleCandidate,
} from '../../core/api/models/reservations';

@Component({
  selector: 'app-reservations',
  imports: [
    CommonModule, FormsModule, RouterLink,
    NzAlertModule, NzBreadCrumbModule, NzButtonModule, NzDatePickerModule, NzEmptyModule,
    NzIconModule, NzInputModule, NzInputNumberModule, NzModalModule, NzSpinModule,
    NzTableModule, NzTagModule,
    TranslatePipe,
  ],
  templateUrl: './reservations.html',
  styleUrl: './reservations.scss',
})
export class Reservations {
  /** Parser polskiego formatu kwot dla pól `nz-input-number` — uzasadnienie przy `parseAmount`. */
  protected readonly parseAmount = parseAmount;

  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly errorMessages = inject(ErrorMessages);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  /** Ta sama nazwa parametru co na liście transakcji i na ekranie oszczędności. */
  protected readonly budgetIds = computed(() => this.queryParams().getAll('budgetId'));

  private readonly resource = httpResource<ReservationsResponse>(() => ({
    url: '/api/savings/reservations',
    params: { ...(this.budgetIds().length > 0 ? { budgetId: this.budgetIds() } : {}) },
  }));

  /** `value()` RZUCA w stanie błędu — patrz core/api/resource-value.ts. */
  private readonly value = valueOf(this.resource);

  protected readonly loading = this.resource.isLoading;
  protected readonly failure = errorOf(this.resource);
  protected readonly busy = signal(false);

  protected readonly data = computed(() => this.value() ?? null);
  protected readonly rows = computed(() => this.data()?.reservations ?? []);

  /**
   * Czy rezerwacje przekraczają stan konta — i o ile.
   *
   * ⚠️ Osobny stan, a nie minus w nagłówku. Ujemne „wolne środki" to poprawna informacja,
   * ale liczba ze znakiem minus przy takiej etykiecie czyta się jak błąd aplikacji,
   * a nie jak stan finansów.
   */
  protected readonly overReserved = computed(() => {
    const free = this.data()?.freeFunds;
    return free !== undefined && free < 0 ? -free : null;
  });

  // ── Dodawanie i edycja ───────────────────────────────────────────────────────────────

  protected readonly editorOpen = signal(false);
  protected readonly editing = signal<SavingsReservation | null>(null);
  protected readonly draftName = signal('');
  protected readonly draftAmount = signal<number | null>(null);
  protected readonly draftDue = signal<Date | null>(null);

  constructor() {
    // Kafel na ekranie oszczędności ma przycisk „Dodaj rezerwację" (makieta 147:96), ale modal
    // żyje TUTAJ — dwie kopie tego samego formularza rozjechałyby się przy pierwszej poprawce.
    // Kafel więc nawiguje z `?add=1`, a ekran otwiera modal.
    if (this.route.snapshot.queryParamMap.get('add') !== null) this.openEditor(null);
  }

  protected openEditor(row: SavingsReservation | null): void {
    this.editing.set(row);
    this.draftName.set(row?.name ?? '');
    this.draftAmount.set(row?.amount ?? null);
    this.draftDue.set(row ? new Date(row.dueMonth) : null);
    this.editorOpen.set(true);
  }

  protected readonly canSave = computed(() =>
    this.draftName().trim().length > 0
    && (this.draftAmount() ?? 0) > 0
    && this.draftDue() !== null);

  protected async save(): Promise<void> {
    if (!this.canSave()) return;

    const body = {
      name: this.draftName().trim(),
      amount: this.draftAmount(),
      dueMonth: this.isoMonth(this.draftDue()!),
      priority: 0,
      budgetId: this.budgetIds()[0] ?? null,
    };

    const existing = this.editing();

    this.busy.set(true);
    try {
      await firstValueFrom(existing
        ? this.http.put(`/api/savings/reservations/${existing.id}`, body)
        : this.http.post('/api/savings/reservations', body));
      this.editorOpen.set(false);
      this.message.success(this.translate.instant('reservations.saved'));
      this.resource.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(row: SavingsReservation): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('reservations.deleteConfirm.header', { name: row.name }),
      description: this.translate.instant('reservations.deleteConfirm.description'),
    });
    if (!ok) return;

    this.busy.set(true);
    try {
      await firstValueFrom(this.http.delete(`/api/savings/reservations/${row.id}`));
      this.message.success(this.translate.instant('reservations.deleted'));
      this.resource.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  // ── Rozliczenie ──────────────────────────────────────────────────────────────────────

  protected readonly settleOpen = signal(false);
  protected readonly settling = signal<SavingsReservation | null>(null);
  protected readonly candidates = signal<SettleCandidate[]>([]);
  protected readonly chosenCandidate = signal<string | null>(null);

  /**
   * Otwiera modal „Czy to było ubezpieczenie?".
   *
   * ⚠️ Kandydaci ciągną się DOPIERO tutaj, nie razem z listą — inaczej każdy wiersz odpalałby
   * własne zapytanie po transakcjach, żeby najczęściej nie pokazać nic.
   */
  protected async openSettle(row: SavingsReservation): Promise<void> {
    this.settling.set(row);
    this.candidates.set([]);
    this.chosenCandidate.set(null);
    this.settleOpen.set(true);

    this.busy.set(true);
    try {
      const found = await firstValueFrom(this.http.get<SettleCandidate[]>(
        `/api/savings/reservations/${row.id}/settle-candidates`));
      this.candidates.set(found);
      // Pierwszy jest najlepiej dopasowany kwotą — ale i tak MUSI go potwierdzić człowiek.
      this.chosenCandidate.set(found[0]?.id ?? null);
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async settle(): Promise<void> {
    const row = this.settling();
    const transactionId = this.chosenCandidate();
    if (!row || !transactionId) return;

    this.busy.set(true);
    try {
      await firstValueFrom(this.http.post(
        `/api/savings/reservations/${row.id}/settle`, { transactionId }));
      this.settleOpen.set(false);
      this.message.success(this.translate.instant('reservations.settled'));
      this.resource.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async unsettle(row: SavingsReservation): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('reservations.unsettleConfirm.header', { name: row.name }),
      description: this.translate.instant('reservations.unsettleConfirm.description'),
    });
    if (!ok) return;

    this.busy.set(true);
    try {
      await firstValueFrom(this.http.delete(`/api/savings/reservations/${row.id}/settle`));
      this.message.success(this.translate.instant('reservations.unsettled'));
      this.resource.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  // ── Formatowanie ─────────────────────────────────────────────────────────────────────

  protected reload(): void {
    this.resource.reload();
  }

  protected errorText(error: unknown): string {
    return this.errorMessages.of(error);
  }

  protected money(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
      .format(value);
  }

  protected monthLabel(iso: string | null): string {
    if (iso === null) return '—';
    const [year, month] = iso.split('-');
    return new Intl.DateTimeFormat('pl-PL', { month: 'long', year: 'numeric' })
      .format(new Date(Number(year), Number(month) - 1, 1));
  }

  protected dateLabel(iso: string): string {
    const [year, month, day] = iso.split('-').map(Number);
    return new Intl.DateTimeFormat('pl-PL').format(new Date(year, month - 1, day));
  }

  /** Ile procent rezerwacji jest pokryte — do pierścienia i do paska w tabeli. */
  protected percent(row: SavingsReservation): number {
    return row.amount <= 0 ? 0 : Math.round((row.collected / row.amount) * 100);
  }

  /**
   * Pierwszy dzień wybranego miesiąca w ISO, LOKALNIE.
   *
   * ⚠️ Nie `toISOString()` — ten przelicza na UTC, więc 1 marca 00:00 w strefie CET wychodzi
   * jako 28 lutego 23:00, czyli termin cofa się o miesiąc. Serwer i tak normalizuje datę
   * do pierwszego dnia, ale normalizowałby już ZŁY miesiąc.
   */
  private isoMonth(date: Date): string {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-01`;
  }
}
