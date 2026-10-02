import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, httpResource } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropdownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSegmentedModule } from 'ng-zorro-antd/segmented';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmDialogService } from '../../../core/confirm-dialog/confirm-dialog.service';
import { ErrorMessages } from '../../../core/errors/error-messages';
import { valueOf } from '../../../core/api/resource-value';
import { CategoryRequest, CategoryType, ManagedCategory } from '../../../core/api/models/managed-category';

/** Najdłuższa nazwa, jaką mieści kolumna w bazie — backend odrzuca dłuższe, tu tylko ostrzegamy wcześniej. */
const NameMaxLength = 100;

/**
 * Zakładka „Kategorie" — własne kategorie konta obok wspólnych (tylko do odczytu).
 *
 * Osobny komponent z tego samego powodu co `Rules`: to inny slice domeny niż budżety, dzielący z nimi wyłącznie
 * miejsce na ekranie. Zasady (nazwa unikalna wśród własnych i wspólnych, typ zablokowany po użyciu, usunięcie
 * tylko nieużywanej) egzekwuje backend — front pokazuje je z góry, ale jego 400/409 pokazuje, nie ukrywa.
 */
@Component({
  selector: 'app-categories',
  imports: [
    FormsModule,
    NzAlertModule, NzButtonModule, NzDropdownModule, NzIconModule, NzInputModule, NzModalModule,
    NzSegmentedModule, NzSpinModule, NzTableModule, TranslatePipe,
  ],
  templateUrl: './categories.html',
  styleUrl: './categories.scss',
})
export class Categories {
  private readonly http = inject(HttpClient);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly errorMessages = inject(ErrorMessages);
  private readonly confirmDialog = inject(ConfirmDialogService);

  private readonly resource = httpResource<ManagedCategory[]>(() => '/api/categories/manage');

  /** Bezpieczny odczyt — `value()` RZUCA w stanie błędu (patrz core/api/resource-value.ts). */
  private readonly resourceValue = valueOf(this.resource);

  protected readonly loading = this.resource.isLoading;
  protected readonly categories = computed(() => this.resourceValue() ?? []);

  protected readonly nameMaxLength = NameMaxLength;

  protected readonly typeOptions = computed(() => [
    { label: this.translate.instant('settings.categories.type.Expense'), value: 'Expense' },
    { label: this.translate.instant('settings.categories.type.Income'), value: 'Income' },
  ]);

  // ── Edytor ─────────────────────────────────────────────────────────────────────────

  protected readonly editorOpen = signal(false);
  protected readonly saving = signal(false);

  /** `null` znaczy „tworzymy nową", inaczej edytujemy kategorię o tym identyfikatorze. */
  protected readonly editedId = signal<string | null>(null);
  protected readonly editedInUse = signal(false);
  protected readonly name = signal('');
  protected readonly type = signal<CategoryType>('Expense');

  /** Komunikat z API dla otwartego edytora (np. nazwa zajęta) — w modalu, bo toast znika spod maski. */
  protected readonly editorError = signal<string | null>(null);

  protected readonly editorTitle = computed(() =>
    this.editedId() === null ? 'settings.categories.editor.createTitle' : 'settings.categories.editor.editTitle');

  /** Typ jest zablokowany dla kategorii, która jest już używana — backend odpowie 409. */
  protected readonly typeLocked = computed(() => this.editedId() !== null && this.editedInUse());

  protected readonly canSave = computed(() => {
    const trimmed = this.name().trim();
    return trimmed.length > 0 && trimmed.length <= NameMaxLength;
  });

  protected openCreate(): void {
    this.editedId.set(null);
    this.editedInUse.set(false);
    this.name.set('');
    this.type.set('Expense');
    this.editorError.set(null);
    this.editorOpen.set(true);
  }

  protected openEdit(category: ManagedCategory): void {
    this.editedId.set(category.id);
    this.editedInUse.set(category.inUse);
    this.name.set(category.name);
    this.type.set(category.type);
    this.editorError.set(null);
    this.editorOpen.set(true);
  }

  protected closeEditor(): void {
    this.editorOpen.set(false);
  }

  protected async save(): Promise<void> {
    if (!this.canSave() || this.saving()) return;

    const body: CategoryRequest = { name: this.name().trim(), type: this.type() };
    const id = this.editedId();

    this.saving.set(true);
    this.editorError.set(null);
    try {
      if (id === null) {
        await firstValueFrom(this.http.post<ManagedCategory>('/api/categories', body));
      } else {
        await firstValueFrom(this.http.put<ManagedCategory>(`/api/categories/${id}`, body));
      }
      this.editorOpen.set(false);
      this.resource.reload();
      this.message.info(this.translate.instant(
        id === null ? 'settings.categories.toast.created' : 'settings.categories.toast.updated'));
    } catch (error) {
      this.editorError.set(this.errorMessages.of(error));
    } finally {
      this.saving.set(false);
    }
  }

  // ── Usuwanie ───────────────────────────────────────────────────────────────────────

  /**
   * Usuwa kategorię po potwierdzeniu.
   *
   * Pozycja menu jest zablokowana dla kategorii używanej (podpowiedź mówi dlaczego), a backend i tak zwróci 409,
   * gdyby ktoś użył jej w międzyczasie — jego komunikat idzie do toasta.
   */
  protected async remove(category: ManagedCategory): Promise<void> {
    const confirmed = await this.confirmDialog.confirm({
      header: this.translate.instant('settings.categories.delete.header', { name: category.name }),
      description: this.translate.instant('settings.categories.delete.description'),
      confirmText: this.translate.instant('settings.categories.delete.confirm'),
      danger: true,
    });
    if (!confirmed) return;

    try {
      await firstValueFrom(this.http.delete(`/api/categories/${category.id}`));
      this.resource.reload();
      this.message.info(this.translate.instant('settings.categories.toast.deleted'));
    } catch (error) {
      this.message.error(this.errorMessages.of(error));
    }
  }

  // ── Formatowanie ───────────────────────────────────────────────────────────────────

  /** Użycie jako jedno zdanie; pusty tekst dla kategorii, której nikt jeszcze nie używa. */
  protected usage(category: ManagedCategory): string {
    return this.translate.instant('settings.categories.usage', {
      transactions: category.transactions,
      rules: category.rules,
      limits: category.limits,
    });
  }
}
