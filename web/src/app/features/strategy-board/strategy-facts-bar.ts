import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Pasek nad tablicą: ile zdarzeń już nastąpiło (makieta Figma „Strategia”: 423:1016). Zielona kropka to zdarzenie liczone
 * z faktu, czarna — z planu. Nic nie trzyma: liczby podaje tablica.
 */
@Component({
  selector: 'app-strategy-facts-bar',
  imports: [TranslatePipe],
  templateUrl: './strategy-facts-bar.html',
  styleUrl: './strategy-facts-bar.scss',
})
export class StrategyFactsBar {
  readonly done = input(0);
  readonly total = input(0);
}
