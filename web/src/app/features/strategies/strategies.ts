import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ActiveBudget } from '../../core/active-budget';
import { BudgetSwitcher } from '../../core/budget-switcher/budget-switcher';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ErrorMessages } from '../../core/errors/error-messages';
import { errorOf, valueOf } from '../../core/api/resource-value';
import {
  CreateStrategyRequest, StrategiesResponse, Strategy, StrategyListItem, StrategyTemplate,
} from '../../core/api/models/strategies';

/** Najdłuższa nazwa strategii — ta sama co `StrategyGraphValidator.MaxNameLength` w API. */
const MAX_NAME_LENGTH = 100;

/**
 * Ekran „Wybierz strategię” (makieta Figma, strona „Strategia”: 381:453, zgłoszenie #27).
 *
 * Lista strategii jednego budżeta, od ostatnio zmienionej, z założeniem nowej — pustej albo z szablonu „kredyt
 * i poduszka”. Sama strategia (tablica) jest na osobnym ekranie `/strategies/:id`.
 *
 * ⚠️ Nie ma jeszcze „Duplikuj” z makiety: API nie ma takiej operacji, a składanie jej z trzech żądań po stronie
 * klienta rozjeżdżałoby się przy pierwszym błędzie w środku. Wróci razem z nowym endpointem.
 */
@Component({
  selector: 'app-strategies',
  imports: [
    DatePipe, FormsModule, RouterLink, BudgetSwitcher,
    NzAlertModule, NzBreadCrumbModule, NzButtonModule, NzInputModule, NzModalModule, NzSpinModule,
    TranslatePipe,
  ],
  templateUrl: './strategies.html',
  styleUrl: './strategies.scss',
})
export class Strategies {
  protected readonly maxNameLength = MAX_NAME_LENGTH;

  /** Język nazw miesięcy w szablonie — `DatePipe` bez tego pokazuje angielskie. */
  protected locale(): string {
    return this.translate.currentLang() || 'pl';
  }

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

  /** JEDEN budżet: z adresu, a gdy adres milczy — ten z widoku (`ActiveBudget`, issue #16). */
  protected readonly budgetId = computed(() => {
    const fromUrl = this.budgetIdFromUrl();
    return this.activeBudget.resolve(fromUrl ? [fromUrl] : [])[0] ?? null;
  });

  private readonly publishBudgetFromUrl = effect(() => {
    const fromUrl = this.budgetIdFromUrl();
    if (fromUrl) this.activeBudget.set([fromUrl]);
  });

  private readonly resource = httpResource<StrategiesResponse>(() => ({
    url: '/api/strategies',
    params: { ...(this.budgetId() ? { budgetId: this.budgetId()! } : {}) },
  }));

  /** `value()` RZUCA w stanie błędu — patrz core/api/resource-value.ts. */
  private readonly value = valueOf(this.resource);

  protected readonly loading = this.resource.isLoading;
  protected readonly failure = errorOf(this.resource);
  protected readonly busy = signal(false);

  /** Ostatnia odpowiedź — żeby przełącznik budżetu i lista nie migały pustką przy przeładowaniu. */
  protected readonly lastData = linkedSignal<StrategiesResponse | undefined, StrategiesResponse | null>({
    source: this.value,
    computation: (next, prev) => next ?? prev?.value ?? null,
  });

  protected readonly strategies = computed(() => this.lastData()?.strategies ?? []);
  protected readonly noBudget = computed(() => {
    const d = this.lastData();
    return d !== null && d.selectedBudgetId === null;
  });

  // ── Nowa strategia ───────────────────────────────────────────────────────────────────

  protected readonly creating = signal<StrategyTemplate | null>(null);
  protected readonly newName = signal('');
  protected readonly newNameValid = computed(() => {
    const name = this.newName().trim();
    return name.length > 0 && name.length <= MAX_NAME_LENGTH;
  });

  protected openCreate(template: StrategyTemplate): void {
    this.newName.set(this.translate.instant(template === 'LoanAndCushion' ? 'strategies.create.defaultNameLoan' : 'strategies.create.defaultNameBlank'));
    this.creating.set(template);
  }

  protected closeCreate(): void {
    this.creating.set(null);
  }

  protected async create(): Promise<void> {
    const template = this.creating();
    if (!template || !this.newNameValid()) return;
    const body: CreateStrategyRequest = {
      budgetId: this.lastData()?.selectedBudgetId ?? null,
      name: this.newName().trim(),
      template,
    };

    this.busy.set(true);
    try {
      const created = await firstValueFrom(this.http.post<Strategy>('/api/strategies', body));
      this.creating.set(null);
      await this.router.navigate(['/strategies', created.id]);
    } catch (err) {
      this.message.error(this.errorMessages.of(err, 'strategies.errors.saveFailed'));
    } finally {
      this.busy.set(false);
    }
  }

  // ── Usuwanie ─────────────────────────────────────────────────────────────────────────

  protected async remove(row: StrategyListItem): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('strategies.removeConfirm.header', { name: row.name }),
      description: this.translate.instant('strategies.removeConfirm.description', {
        events: row.eventCount, actions: row.actionCount,
      }),
      confirmText: this.translate.instant('strategies.remove'),
      danger: true,
    });
    if (!ok) return;

    this.busy.set(true);
    try {
      await firstValueFrom(this.http.delete(`/api/strategies/${row.id}`));
      this.message.success(this.translate.instant('strategies.removed'));
      this.resource.reload();
    } catch (err) {
      this.message.error(this.errorMessages.of(err, 'strategies.errors.saveFailed'));
    } finally {
      this.busy.set(false);
    }
  }

  // ── Nawigacja ────────────────────────────────────────────────────────────────────────

  /** Wybór z przełącznika idzie do ADRESU — patrz `BudgetSwitcher`, dlaczego nie wprost do `ActiveBudget`. */
  protected switchBudget(id: string): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: { budgetId: id }, queryParamsHandling: 'merge' });
  }

  protected reload(): void {
    this.resource.reload();
  }

  protected errorText(err: unknown): string {
    return this.errorMessages.of(err, 'strategies.errors.loadFailed');
  }
}
