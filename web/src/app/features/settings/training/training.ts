import { Component, computed, inject, signal } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmDialogService } from '../../../core/confirm-dialog/confirm-dialog.service';
import { ErrorMessages } from '../../../core/errors/error-messages';
import { valueOf } from '../../../core/api/resource-value';
import {
  ModelVersion,
  RecategorizeReport,
  TrainingReport,
  TrainingSetOverview,
} from '../../../core/api/models/training-set-overview';

/**
 * Zakładka „Dane treningowe" — z czego uczy się model i jak go douczyć.
 *
 * Osobny komponent, nie kolejna sekcja w `Settings`: tamten ma już ~500 linii wokół budżetów,
 * a to jest inny slice domeny (kategoryzacja), który dzieli z nim wyłącznie miejsce na ekranie.
 */
@Component({
  selector: 'app-training',
  imports: [
    RouterLink,
    NzAlertModule, NzButtonModule, NzIconModule, NzSpinModule,
    NzStatisticModule, NzTableModule, NzTagModule,
    TranslatePipe,
  ],
  templateUrl: './training.html',
  styleUrl: './training.scss',
})
export class Training {
  private readonly http = inject(HttpClient);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly errorMessages = inject(ErrorMessages);
  private readonly confirmDialog = inject(ConfirmDialogService);

  private readonly overview = httpResource<TrainingSetOverview>(
    () => '/api/categorization/training-set',
  );

  /** Bezpieczny odczyt — `value()` RZUCA w stanie bledu (patrz core/api/resource-value.ts). */
  private readonly overviewValue = valueOf(this.overview);

  protected readonly loading = this.overview.isLoading;
  protected readonly data = computed(() => this.overviewValue() ?? null);

  /** Trwa trening albo przywracanie — obie operacje dotykają tego samego pliku modelu. */
  protected readonly busy = signal(false);

  /**
   * Raport z treningu uruchomionego W TEJ SESJI. Świadomie osobno od historii: po kliknięciu
   * „Doucz model" użytkownik chce zobaczyć wynik TEGO uruchomienia, a nie szukać go na liście.
   */
  protected readonly lastRun = signal<TrainingReport | null>(null);

  /**
   * Wynik przeliczenia kategorii z TEJ sesji. Tak samo jak przy treningu: po kliknięciu
   * użytkownik chce zobaczyć, co się właśnie stało z jego danymi.
   */
  protected readonly lastRecategorization = signal<RecategorizeReport | null>(null);

  protected readonly models = computed(() => this.data()?.models ?? []);
  protected readonly categories = computed(() => this.data()?.categories ?? []);

  /**
   * Kategorie bez ani jednego przykładu. Wydzielone, bo to nie jest „mała liczba", tylko
   * inny stan jakościowy: model nigdy takiej kategorii nie wskaże, cokolwiek by się działo.
   */
  protected readonly emptyCategories = computed(
    () => this.categories().filter((c) => c.count === 0),
  );

  /** Najliczniejsza kategoria — punkt odniesienia dla słupków w kolumnie „Udział". */
  protected readonly maxCategoryCount = computed(
    () => this.categories().reduce((max, c) => Math.max(max, c.count), 0),
  );

  protected barWidth(count: number): string {
    const max = this.maxCategoryCount();
    return max === 0 ? '0%' : `${Math.round((count / max) * 100)}%`;
  }

  protected async train(): Promise<void> {
    this.busy.set(true);
    try {
      const report = await firstValueFrom(
        this.http.post<TrainingReport>('/api/categorization/train', {}),
      );
      this.lastRun.set(report);
      this.message.success(this.translate.instant('settings.training.trainSuccess', {
        rows: report.rows,
      }));
      this.overview.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Przelicza kategorie wierszy, które są już w bazie.
   *
   * Osobny przycisk, nie część treningu: trening produkuje model, to przepisuje dane.
   * Sklejenie ich znaczyłoby, że nie da się nauczyć modelu bez przepisania historii —
   * a to dwie różne zgody.
   */
  protected async recategorize(): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('settings.training.recategorizeConfirm.header'),
      description: this.translate.instant('settings.training.recategorizeConfirm.description'),
    });
    if (!ok) return;

    this.busy.set(true);
    try {
      const report = await firstValueFrom(
        this.http.post<RecategorizeReport>('/api/categorization/recategorize', {}),
      );
      this.lastRecategorization.set(report);
      this.message.success(this.translate.instant('settings.training.recategorizeSuccess', {
        recategorized: report.recategorized,
        examined: report.examined,
      }));
      // Kolejka przeglądu mogła urosnąć — licznik na tym ekranie musi to pokazać.
      this.overview.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async restore(model: ModelVersion): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('settings.training.restoreConfirm.header'),
      description: this.translate.instant('settings.training.restoreConfirm.description'),
    });
    if (!ok) return;

    this.busy.set(true);
    try {
      await firstValueFrom(
        this.http.post('/api/categorization/activate', { version: model.version }),
      );
      // Raport z bieżącej sesji przestaje opisywać aktywny model — zostawiony wprowadzałby w błąd.
      this.lastRun.set(null);
      this.message.success(this.translate.instant('settings.training.restoreSuccess'));
      this.overview.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Procent bez miejsc po przecinku — dokładniejszy zapis sugerowałby precyzję, której tu nie ma. */
  protected percent(value: number): string {
    return `${Math.round(value * 100)}%`;
  }

  protected moment(iso: string): string {
    const date = new Date(iso);
    return new Intl.DateTimeFormat('pl-PL', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    }).format(date);
  }

}
