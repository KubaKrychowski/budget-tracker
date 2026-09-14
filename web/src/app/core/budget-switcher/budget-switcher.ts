import { Component, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TranslatePipe } from '@ngx-translate/core';
import { BudgetOption } from '../api/models/budget-option';

/**
 * „Budżet: [wybór ▾]" nad ekranem, który liczy na JEDNYM budżecie — cele oszczędzania, rezerwacje
 * (a potem limity). Ten sam wygląd co przełącznik dashboardu (makieta 68:2387).
 *
 * ⚠️ Komponent tylko POKAZUJE wybór i zgłasza zmianę — nie zapisuje jej nigdzie sam. Budżet ekranu
 * jeździ w adresie (`?budgetId`), a `ActiveBudget` dostaje go z adresu (issue #16). Gdyby przełącznik
 * ustawiał `ActiveBudget` bezpośrednio, adres z poprzednim `budgetId` dalej by wygrywał
 * (`ActiveBudget.resolve`) i wybór nie zmieniłby na ekranie niczego.
 *
 * Wybranym jest budżet, na którym serwer FAKTYCZNIE policzył odpowiedź (`selectedBudgetIds`), a nie
 * ten z adresu — bez parametru serwer bierze domyślny i przełącznik ma pokazać właśnie jego.
 */
@Component({
  selector: 'app-budget-switcher',
  imports: [FormsModule, NzSelectModule, NzTagModule, TranslatePipe],
  template: `
    <div class="bsw">
      <label class="bsw__label" for="budget-switcher">{{ 'dashboard.budgetLabel' | translate }}</label>
      <nz-select
        nzId="budget-switcher"
        class="bsw__control"
        [nzPlaceHolder]="'dashboard.budgetPlaceholder' | translate"
        [nzDisabled]="budgets().length === 0"
        [ngModel]="selectedId()"
        (ngModelChange)="pick($event)">
        @for (b of budgets(); track b.id) {
          <nz-option [nzValue]="b.id" [nzLabel]="b.name" nzCustomContent>
            {{ b.name }}
            @if (b.disabled) {
              <nz-tag nzColor="warning">{{ 'dashboard.budgetDisabledTag' | translate }}</nz-tag>
            }
          </nz-option>
        }
      </nz-select>
      <!-- Znacznik POZA listą — nzCustomContent dotyczy tylko rozwiniętej listy. -->
      @if (selectedDisabled()) {
        <nz-tag nzColor="warning">{{ 'dashboard.budgetDisabledTag' | translate }}</nz-tag>
      }
    </div>
  `,
  styles: `
    /* Bez marginesów — odstęp ustala ekran: na celach to wiersz pod okruszkami, na limitach wnętrze karty „Konfiguracja". */
    :host { display: block; }
    .bsw { display: flex; align-items: center; gap: 12px; }
    .bsw__label { white-space: nowrap; }
    .bsw__control { min-width: 240px; }
  `,
})
export class BudgetSwitcher {
  readonly budgets = input.required<readonly BudgetOption[]>();
  readonly selectedId = input<string | null>(null);

  /** Użytkownik wybrał INNY budżet. Wybór tego samego nie zgłasza niczego. */
  readonly budgetChange = output<string>();

  protected readonly selectedDisabled = computed(
    () => this.budgets().find((b) => b.id === this.selectedId())?.disabled ?? false,
  );

  protected pick(id: string | null): void {
    if (id && id !== this.selectedId()) this.budgetChange.emit(id);
  }
}
