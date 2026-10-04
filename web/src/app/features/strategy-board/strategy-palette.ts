import { Component, output } from '@angular/core';
import { FFlowModule } from '@foblex/flow';
import { TranslatePipe } from '@ngx-translate/core';
import { StrategyNodeType } from '../../core/api/models/strategies';
import { CATEGORIES, CATEGORY_ORDER, NodeCategory } from './strategy-node-meta';

/** Pozycja palety: kategoria i rodzaj, z jakim powstaje nowy kafelek (zmienia się w ustawieniach kafelka). */
interface PaletteItem {
  readonly category: NodeCategory;
  readonly type: StrategyNodeType;
}

/**
 * Panel „Dodaj kafelek” (makieta Figma „Strategia”: 388:896) — trzy kategorie kafelków do przeciągnięcia na tablicę
 * albo dodania przyciskiem „+”. Konkretny rodzaj (nadpłata, cel oszczędzania…) wybiera się potem selectem
 * w ustawieniach kafelka, więc paleta nie rośnie razem z liczbą rodzajów.
 *
 * ⚠️ Element z `fExternalItem` jest poza `<f-flow>` w DOM, ale biblioteka łączy go z tablicą przez wstrzykiwanie —
 * dlatego komponent MUSI stać w szablonie komponentu, który ma `provideFFlow` w `providers`. Samo upuszczenie obsługuje
 * rodzic (`fCreateNode`); stąd tu tylko `add` dla przycisku „+”.
 */
@Component({
  selector: 'app-strategy-palette',
  imports: [FFlowModule, TranslatePipe],
  templateUrl: './strategy-palette.html',
  styleUrl: './strategy-palette.scss',
})
export class StrategyPalette {
  readonly add = output<StrategyNodeType>();

  protected readonly items: readonly PaletteItem[] = CATEGORY_ORDER.map((category) => ({
    category,
    type: CATEGORIES[category].default,
  }));
}
