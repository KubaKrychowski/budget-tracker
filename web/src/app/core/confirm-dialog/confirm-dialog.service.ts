import { Injectable, inject } from '@angular/core';
import { NzModalService } from 'ng-zorro-antd/modal';
import { TranslateService } from '@ngx-translate/core';
import { ConfirmDialog } from './confirm-dialog';
import { ConfirmDialogOptions } from './confirm-dialog-options';

/**
 * Jeden modal potwierdzenia dla całej aplikacji, zamiast osobnego `<nz-modal>`
 * (i osobnego kompletu sygnałów `dialog`/`target`/…) przy każdej akcji, która
 * powinna dopytać przed wykonaniem. Tytuł jest zawsze ten sam — „Potwierdź operację" —
 * wołający podaje tylko pytanie (`header`) i opcjonalnie rozwinięcie (`description`)
 * oraz, przy operacjach nieodwracalnych, `confirmKey` do przepisania.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly modal = inject(NzModalService);
  private readonly translate = inject(TranslateService);

  /**
   * @returns `true`, gdy użytkownik potwierdził; `false` przy Anuluj, X w rogu
   * lub kliknięciu w maskę poza modalem — wołający NIE musi rozróżniać tych trzech,
   * bo dla niego znaczą to samo: „nie rób nic".
   */
  confirm(options: ConfirmDialogOptions): Promise<boolean> {
    const ref = this.modal.create<ConfirmDialog, ConfirmDialogOptions>({
      nzTitle: this.translate.instant('confirmDialog.title'),
      nzContent: ConfirmDialog,
      nzData: options,
      nzFooter: null,
      nzMaskClosable: false,
      nzWidth: 572,
    });

    return new Promise((resolve) => {
      ref.afterClose.subscribe((result: boolean | undefined) => resolve(result === true));
    });
  }
}
