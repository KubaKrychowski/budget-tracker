import { Component, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropdownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzTableModule } from 'ng-zorro-antd/table';
import { TranslatePipe } from '@ngx-translate/core';
import { RangeFilterState } from './range-filter-state';

/**
 * Nagłówek kolumny z filtrem „od–do": etykieta (przez `<ng-content>`) + ikona filtra +
 * panel z dwoma polami liczbowymi. Wydzielone z `settings.html`, gdzie ten sam blok
 * powtarzał się cztery razy (Balans, Balans początkowy, Miesięczny limit, Ilość transakcji)
 * różniąc się tylko instancją stanu i precyzją/minimum pola.
 *
 * Rodzic wciąż trzyma `nzCustomFilter` i `[nzSortFn]` na samym `<th>` — te muszą siedzieć
 * bezpośrednio na elemencie, którym operuje `nz-table`. Ten komponent dostarcza tylko
 * TREŚĆ tego `<th>`.
 */
@Component({
  selector: 'app-range-filter',
  imports: [
    FormsModule, NzButtonModule, NzDropdownModule, NzIconModule,
    NzInputNumberModule, NzTableModule, TranslatePipe,
  ],
  templateUrl: './range-filter.html',
  styleUrl: './range-filter.scss',
})
export class RangeFilter {
  readonly filter = input.required<RangeFilterState>();

  /** Kwoty mają grosze (2), liczba transakcji jest zawsze całkowita (0). */
  readonly precision = input(2);

  /** Dolne ograniczenie samego pola liczbowego — np. 0 dla liczby transakcji. */
  readonly min = input<number | null>(null);
}
