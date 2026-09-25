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
  TrainingQueued,
  TrainingReport,
  TrainingSetOverview,
  TrainingStatus,
} from '../../../core/api/models/training-set-overview';

/** Klucz pamięci przeglądarki: czy użytkownik zamknął ostrzeżenie o trafnościach. */
const CAVEAT_ACK_KEY = "budget-tracker:training-metrics-caveat";

/** Odczyt zgodny z konwencją `active-budget.ts`: brak wpisu, śmieci i wyłączony storage znaczą to samo. */
function caveatAcknowledged(): boolean {
  try {
    return localStorage.getItem(CAVEAT_ACK_KEY) === "1";
  } catch {
    return false;
  }
}

function rememberCaveatAcknowledged(): void {
  try {
    localStorage.setItem(CAVEAT_ACK_KEY, "1");
  } catch {
    // Prywatne okno albo wyłączony storage — ostrzeżenie wróci przy następnym wejściu i tyle.
  }
}

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
   * Czy ostrzeżenie o trafnościach jest schowane.
   *
   * ⚠️ Zamknięcie jest PAMIĘTANE (localStorage), nie tylko na czas wizyty: ostrzeżenie mówi o stałej
   * właściwości metryk, a nie o zdarzeniu, więc pokazane po raz dziesiąty temu samemu człowiekowi
   * uczy pomijania alertów w ogóle. Pamięć jest per przeglądarka, tak jak wybrany budżet.
   */
  protected readonly caveatHidden = signal(caveatAcknowledged());

  /** „Rozumiem" — chowa ostrzeżenie i zapamiętuje tę decyzję. */
  protected acknowledgeCaveat(): void {
    this.caveatHidden.set(true);
    rememberCaveatAcknowledged();
  }

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

  /**
   * Zgłoszenie treningu, na którego wynik jeszcze czekamy; `null` = nie ma o co pytać.
   *
   * ⚠️ Żyje tylko w pamięci karty. Przeładowanie strony gubi numer zgłoszenia i to jest w porządku:
   * gotowy trening i tak widać na liście wersji, a ten alert jest wygodą, nie źródłem prawdy.
   */
  protected readonly queuedJobId = signal<string | null>(null);

  /** Trwa sprawdzanie wyniku — żeby dwa kliknięcia nie wysłały dwóch pytań. */
  protected readonly checkingStatus = signal(false);

  /**
   * Zgłasza trening do kolejki i NA TYM KOŃCZY.
   *
   * ⚠️ Świadomie bez odpytywania w pętli: trening bywa długi, a ekran, który sam wali do serwera co
   * kilka sekund, robi to także wtedy, gdy nikt na niego nie patrzy. Wynik sprawdza człowiek, wtedy
   * gdy go potrzebuje — przyciskiem na alercie.
   */
  protected async train(): Promise<void> {
    this.busy.set(true);
    try {
      const queued = await firstValueFrom(
        this.http.post<TrainingQueued>('/api/categorization/train', {}),
      );
      this.queuedJobId.set(queued.jobId);
      this.lastRun.set(null);
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Pyta RAZ o wynik zgłoszonego treningu.
   *
   * ⚠️ Brak wyniku znaczy „czeka w kolejce, trwa albo się nie powiódł” — tego się stąd nie rozróżni,
   * więc komunikat mówi tylko tyle, ile wiadomo. Nieudane przebiegi widać w panelu zadań.
   */
  protected async checkTrainingStatus(): Promise<void> {
    const jobId = this.queuedJobId();
    if (jobId === null || this.checkingStatus()) return;

    this.checkingStatus.set(true);
    try {
      const status = await firstValueFrom(
        this.http.get<TrainingStatus>(`/api/categorization/train/${jobId}`),
      );

      if (!status.ready || status.report === null) {
        this.message.info(this.translate.instant('settings.training.stillRunning'));
        return;
      }

      this.lastRun.set(status.report);
      this.queuedJobId.set(null);
      this.message.success(this.translate.instant('settings.training.trainSuccess', {
        rows: status.report.rows,
      }));
      this.overview.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.checkingStatus.set(false);
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
        this.http.post('/api/categorization/activate', { versionId: model.id }),
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
