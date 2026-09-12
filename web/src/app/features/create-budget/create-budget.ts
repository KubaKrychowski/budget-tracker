import { Component, computed, inject, signal } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzStepsModule } from 'ng-zorro-antd/steps';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DictionaryEntry } from '../../core/api/models/dictionary-entry';
import { CreatedBudget } from '../../core/api/models/created-budget';
import { PageHeader } from '../../core/page-header/page-header';
import { ErrorMessages } from '../../core/errors/error-messages';
import { valueOf } from '../../core/api/resource-value';
import { parseAmount } from '../../core/parse-amount';

/**
 * Kreator budżetu, krok 1 — „Podstawowe informacje" (Figma: `46:1183`).
 *
 * ⚠️ Makieta deklaruje stepper w wariancie „Item Count=3", ale treść ma WYŁĄCZNIE krok 1;
 * kroki 2 i 3 są instancjami bez tytułu i opisu. Renderujemy więc tylko to, co jest
 * zaprojektowane — wymyślanie nazw brakujących kroków byłoby zgadywaniem. Stąd też
 * przycisk główny to „Zakończ", a nie „Dalej": tak jest w makiecie.
 */
@Component({
  selector: 'app-create-budget',
  imports: [
    FormsModule, RouterLink,
    NzAlertModule, NzBreadCrumbModule, NzButtonModule, NzInputModule, NzInputNumberModule,
    NzSelectModule, NzSpinModule, NzStepsModule, TranslatePipe, PageHeader,
  ],
  templateUrl: './create-budget.html',
  styleUrl: './create-budget.scss',
})
export class CreateBudget {
  /** Parser polskiego formatu kwot dla pól `nz-input-number` — uzasadnienie przy `parseAmount`. */
  protected readonly parseAmount = parseAmount;

  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly errorMessages = inject(ErrorMessages);

  /**
   * Komunikat błędu przychodzi GOTOWY z API — backend trzyma teksty w `.resx` i tłumaczy
   * je wg kultury żądania. Front dokłada tylko własny komunikat na awarię sieci.
   */
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  // ── Dane podstawowe ──────────────────────────────────────────────────────────────────

  protected readonly name = signal('');

  /**
   * Waluta domyślnie PLN — makieta pokazuje ją jako wybraną i to jedyna, którą backend
   * przyjmuje. Lista kodów przychodzi z API, nazwy składa front z `currencies.*`,
   * tak samo jak nazwy banków przy imporcie.
   */
  protected readonly currency = signal('PLN');

  /**
   * Bilans początkowy budżetu — punkt odniesienia dla stanu na dany dzień.
   * Ujemny jest dozwolony: debet na koncie to legalny stan startowy.
   *
   * ⚠️ Pola nie ma w makiecie; dołożone, bo budżet ma własny bilans.
   */
  protected readonly initialBalance = signal(0);

  private readonly optionsResource = httpResource<readonly DictionaryEntry[]>(
    () => '/api/budgets/currencies',
  );

  /** Bezpieczny odczyt — `value()` RZUCA w stanie bledu (patrz core/api/resource-value.ts). */
  private readonly optionsResourceValue = valueOf(this.optionsResource);

  protected readonly currencies = computed(() =>
    (this.optionsResourceValue() ?? []).map((currency) => currency.code),
  );
  protected readonly optionsLoading = this.optionsResource.isLoading;

  /** „Zakończ" ma sens dopiero, gdy budżet ma nazwę — reszta ma wartość domyślną. */
  protected readonly canFinish = computed(() => this.name().trim().length > 0);

  // ── Akcje ────────────────────────────────────────────────────────────────────────────

  protected async finish(): Promise<void> {
    if (!this.canFinish() || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    try {
      const budget = await firstValueFrom(
        this.http.post<CreatedBudget>('/api/budgets', {
          name: this.name().trim(),
          currency: this.currency(),
          initialBalance: this.initialBalance(),
        }),
      );

      this.message.success(
        this.translate.instant('createBudget.created', { name: budget.name }),
      );
      void this.router.navigate(['/dashboard']);
    } catch (e) {
      this.error.set(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

}
