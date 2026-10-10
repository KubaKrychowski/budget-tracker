import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { StrategyVariant } from '../../core/api/models/strategies';

/**
 * Pasek wariantów nad tablicą (makieta Figma „Strategia”: 423:451) — „Bazowy”, zapisane warianty i „+ Nowy wariant”.
 * Wybór wariantu zmienia tylko widok tablicy (które kafelki są wygaszone i którego wyniku dotyczy chip), nie graf.
 *
 * Komponent nic nie trzyma: wybór i dodanie wracają do tablicy jako zdarzenia. Na telefonie dochodzi „Warianty”, bo
 * zarządzanie nimi (przełączniki, zmiana nazwy, usuwanie) stoi tam w arkuszu od dołu, a nie w panelu bocznym.
 */
@Component({
  selector: 'app-strategy-variant-bar',
  imports: [TranslatePipe],
  templateUrl: './strategy-variant-bar.html',
  styleUrl: './strategy-variant-bar.scss',
})
export class StrategyVariantBar {
  readonly variants = input.required<readonly StrategyVariant[]>();
  readonly activeId = input<string | null>(null);
  readonly canAdd = input(true);
  readonly mobile = input(false);

  readonly picked = output<string | null>();
  readonly added = output<void>();
  readonly manage = output<void>();
}
