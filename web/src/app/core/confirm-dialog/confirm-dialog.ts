import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NZ_MODAL_DATA, NzModalRef } from 'ng-zorro-antd/modal';
import { TranslateService } from '@ngx-translate/core';
import { ConfirmDialogOptions } from './confirm-dialog-options';

/**
 * Treść modala otwieranego przez `ConfirmDialogService`. Tytuł „Potwierdź operację"
 * siedzi w konfiguracji `NzModalService.create()` (patrz serwis), nie tutaj — ten
 * komponent renderuje tylko to, co jest RÓŻNE między wywołaniami: pytanie, opis
 * i opcjonalne pole z kluczem.
 *
 * Własny footer (`nzFooter: null` w serwisie) zamiast `nzOnOk`/`nzOkDisabled`: blokada
 * przycisku zależy od stanu WEWNĄTRZ tego komponentu (czy klucz się zgadza), a `nzOkDisabled`
 * w konfiguracji modala jest ustawiane raz przy otwarciu i nie reaguje na to, co user
 * później wpisze w pole.
 */
@Component({
  selector: 'app-confirm-dialog',
  imports: [FormsModule, NzButtonModule, NzInputModule],
  templateUrl: './confirm-dialog.html',
  styleUrl: './confirm-dialog.scss',
})
export class ConfirmDialog {
  private readonly translate = inject(TranslateService);
  protected readonly data = inject<ConfirmDialogOptions>(NZ_MODAL_DATA);
  private readonly modalRef = inject(NzModalRef<ConfirmDialog, boolean>);

  protected readonly typedKey = signal('');

  protected readonly confirmText = computed(() =>
    this.data.confirmText ?? this.translate.instant('confirmDialog.confirm'));

  protected readonly cancelText = computed(() =>
    this.data.cancelText ?? this.translate.instant('confirmDialog.cancel'));

  /** Bez `confirmKey` nic nie ma do wpisania — przycisk jest gotowy od razu. */
  protected readonly confirmDisabled = computed(() => {
    const key = this.data.confirmKey;
    return key !== undefined && this.typedKey().trim() !== key;
  });

  protected confirm(): void {
    if (this.confirmDisabled()) return;
    this.modalRef.close(true);
  }

  protected cancel(): void {
    this.modalRef.close(false);
  }
}
