import { Component, computed, effect, inject, input, linkedSignal, output, signal, untracked } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Capacitor } from '@capacitor/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzUploadFile, NzUploadModule } from 'ng-zorro-antd/upload';
import { fromIsoDate, toIsoDate } from '../../../core/api/date-param';
import { ReceiptCandidate, ReceiptRead } from '../../../core/api/models/receipts';
import { TransactionListItem } from '../../../core/api/models/transaction-list-item';
import { errorOf, valueOf } from '../../../core/api/resource-value';
import { ErrorMessages } from '../../../core/errors/error-messages';
import { parseAmount } from '../../../core/parse-amount';

/** Musi zgadzać się z `ReceiptFiles.MaxSizeBytes` w API — inaczej za duży plik odbija się dopiero od serwera. */
const MaxSizeBytes = 10 * 1024 * 1024;

/** Te same typy co `ReceiptFiles.Extensions` w API. */
const SupportedTypes = ['image/jpeg', 'image/png', 'image/heic', 'image/heif', 'application/pdf'];

/** Wybór „żadna z nich" na liście kandydatów — nie myli się z identyfikatorem transakcji. */
const NoTransaction = '__brak__';

type Step = 'upload' | 'review';

/** Które z pól OCR wciąż czekają na sprawdzenie przez człowieka. */
interface Uncertain {
  merchant: boolean;
  date: boolean;
  total: boolean;
}

/**
 * Dodawanie paragonu: wgranie pliku → odczyt OCR → ekran weryfikacji z wyborem transakcji (makieta Figma 432:6880).
 *
 * Dwa wejścia: z paska nad tabelą (wtedy kandydaci z importu do wyboru) i z menu wiersza (`transaction` ustawione — paragon
 * trafia do tej transakcji, bez wyboru). Nic nie łączy się samo: kandydaci są podpowiedzią, a przypięcie to decyzja człowieka.
 */
@Component({
  selector: 'app-receipt-dialog',
  imports: [
    FormsModule, NzAlertModule, NzButtonModule, NzDatePickerModule, NzIconModule, NzInputModule, NzInputNumberModule,
    NzModalModule, NzSpinModule, NzTagModule, NzUploadModule, TranslatePipe,
  ],
  templateUrl: './receipt-dialog.html',
  styleUrl: './receipt-dialog.scss',
})
export class ReceiptDialog {
  /** Czy okno jest otwarte — steruje rodzic; zamknięcie zgłasza przez `closed`. */
  readonly open = input.required<boolean>();

  /** Transakcja z menu wiersza; `null` = dodawanie z paska, z wyborem kandydata. */
  readonly transaction = input<TransactionListItem | null>(null);

  /** Budżety listy transakcji — kandydaci szukają w tym samym zasięgu, który użytkownik ma na ekranie. */
  readonly budgetIds = input<readonly string[]>([]);

  readonly closed = output<void>();
  readonly saved = output<void>();

  protected readonly parseAmount = parseAmount;

  private readonly http = inject(HttpClient);
  private readonly message = inject(NzMessageService);
  private readonly translate = inject(TranslateService);
  private readonly errorMessages = inject(ErrorMessages);

  protected readonly noTransaction = NoTransaction;
  /** „Zrób zdjęcie" ma sens tylko w aplikacji mobilnej — w przeglądarce nie ma czego uruchomić. */
  protected readonly isNative = Capacitor.isNativePlatform();

  protected readonly step = signal<Step>('upload');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Odpowiedź OCR; po niej człowiek poprawia pola poniżej. */
  protected readonly read = signal<ReceiptRead | null>(null);
  protected readonly previewUrl = signal<string | null>(null);
  protected readonly previewIsImage = signal(false);

  protected readonly merchant = signal('');
  protected readonly date = signal<Date | null>(null);
  protected readonly total = signal<number | null>(null);
  protected readonly uncertain = signal<Uncertain>({ merchant: false, date: false, total: false });
  protected readonly choice = signal<string | null>(null);

  /** Etykiety pól wciąż do sprawdzenia — składają zdanie w alercie na górze ekranu weryfikacji. */
  protected readonly uncertainFields = computed(() => {
    const u = this.uncertain();
    return [
      u.merchant ? this.translate.instant('transactions.receipt.merchant') : null,
      u.date ? this.translate.instant('transactions.receipt.date') : null,
      u.total ? this.translate.instant('transactions.receipt.total') : null,
    ].filter((f): f is string => f !== null).join(', ').toLowerCase();
  });

  private readonly candidatesResource = httpResource<ReceiptCandidate[]>(() => {
    const date = this.date();
    const total = this.total();
    if (this.step() !== 'review' || this.transaction() !== null || date === null || total === null || total <= 0) {
      return undefined;
    }
    return {
      url: '/api/receipts/candidates',
      params: { budgetId: [...this.budgetIds()], total, date: toIsoDate(date) },
    };
  });
  private readonly candidatesValue = valueOf(this.candidatesResource);
  protected readonly candidatesError = errorOf(this.candidatesResource);
  protected readonly candidates = linkedSignal<ReceiptCandidate[] | undefined, ReceiptCandidate[]>({
    source: this.candidatesValue,
    computation: (fresh, previous) => fresh ?? previous?.value ?? [],
  });

  /** Najlepszy kandydat zaznaczony z góry — użytkownik i tak widzi go na liście i może zmienić wybór. */
  private readonly preselectBest = effect(() => {
    const list = this.candidates();
    if (this.transaction() !== null) return;
    this.choice.set(list.find((c) => c.match === 'Exact')?.id ?? null);
  });

  /**
   * Przy każdym otwarciu okno startuje od wyboru pliku, bez śladu po poprzednim paragonie.
   *
   * ⚠️ `untracked`: `reset()` czyta sygnały (np. adres podglądu). Bez tego efekt śledziłby je i po KAŻDYM wgraniu
   * pliku zerował okno z powrotem do wyboru pliku — odczyt znikał w chwili, gdy się pojawił.
   */
  private readonly resetOnOpen = effect(() => {
    if (this.open()) untracked(() => this.reset());
  });

  protected readonly canSave = computed(() => {
    if (this.busy() || this.read() === null) return false;
    return this.transaction() !== null || this.choice() !== null;
  });

  protected readonly saveLabelKey = computed(() =>
    this.transaction() !== null || (this.choice() !== null && this.choice() !== NoTransaction)
      ? 'transactions.receipt.attach'
      : 'transactions.receipt.saveOnly');

  private reset(): void {
    this.revokePreview();
    this.step.set('upload');
    this.busy.set(false);
    this.error.set(null);
    this.read.set(null);
    this.choice.set(null);
    this.uncertain.set({ merchant: false, date: false, total: false });
  }

  private revokePreview(): void {
    const url = this.previewUrl();
    if (url) URL.revokeObjectURL(url);
    this.previewUrl.set(null);
  }

  /** `nz-upload` bez adresu docelowego — plik idzie na odczyt dopiero po naszej walidacji (jak w imporcie). */
  protected readonly captureFile = (file: NzUploadFile): boolean => {
    const native = (file as unknown as { originFileObj?: File }).originFileObj ?? (file as unknown as File);
    void this.upload(native);
    return false;
  };

  protected pickCameraFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0);
    input.value = '';
    if (file) void this.upload(file);
  }

  private async upload(file: File): Promise<void> {
    this.error.set(null);
    if (file.size > MaxSizeBytes) {
      this.error.set(this.translate.instant('transactions.receipt.tooLarge'));
      return;
    }
    if (!SupportedTypes.includes(file.type)) {
      this.error.set(this.translate.instant('transactions.receipt.unsupported'));
      return;
    }

    this.busy.set(true);
    try {
      const body = new FormData();
      body.append('file', file, file.name);
      const result = await firstValueFrom(this.http.post<ReceiptRead>('/api/receipts', body));

      this.revokePreview();
      this.previewIsImage.set(file.type.startsWith('image/') && file.type !== 'image/heic' && file.type !== 'image/heif');
      this.previewUrl.set(this.previewIsImage() ? URL.createObjectURL(file) : null);
      this.read.set(result);
      this.merchant.set(result.merchant ?? '');
      this.date.set(result.date ? fromIsoDate(result.date) : null);
      this.total.set(result.total);
      this.uncertain.set({ merchant: result.merchantUncertain, date: result.dateUncertain, total: result.totalUncertain });
      this.step.set('review');
    } catch (e) {
      this.error.set(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected money(value: number): string {
    return new Intl.NumberFormat('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2, useGrouping: true }).format(value);
  }

  protected day(iso: string): string {
    const [y, m, d] = iso.split('-');
    return `${d}.${m}.${y}`;
  }

  protected edit(field: keyof Uncertain): void {
    this.uncertain.update((u) => ({ ...u, [field]: false }));
  }

  protected setMerchant(value: string): void {
    this.merchant.set(value);
    this.edit('merchant');
  }

  protected setDate(value: Date | null): void {
    this.date.set(value);
    this.edit('date');
  }

  protected setTotal(value: number | null): void {
    this.total.set(value);
    this.edit('total');
  }

  protected async save(): Promise<void> {
    const read = this.read();
    if (!read || !this.canSave()) return;

    const choice = this.choice();
    const transactionId = this.transaction()?.id ?? (choice && choice !== NoTransaction ? choice : null);

    this.busy.set(true);
    try {
      await firstValueFrom(this.http.put(`/api/receipts/${read.id}`, {
        transactionId,
        merchant: this.merchant().trim() || null,
        date: this.date() ? toIsoDate(this.date()!) : null,
        total: this.total(),
      }));
      this.message.success(this.translate.instant(
        transactionId ? 'transactions.receipt.savedAttached' : 'transactions.receipt.saved'));
      this.saved.emit();
      this.closed.emit();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Anuluj po odczycie kasuje wgrany paragon — plik już leży w magazynie i bez tego zostałby tam bez właściciela w UI.
   * Błąd kasowania nie blokuje zamknięcia okna: użytkownik chciał wyjść.
   */
  protected async cancel(): Promise<void> {
    const read = this.read();
    if (read && this.step() === 'review') {
      try {
        await firstValueFrom(this.http.delete(`/api/receipts/${read.id}`));
      } catch {
        // celowo pusto — patrz opis metody
      }
    }
    this.closed.emit();
  }
}
