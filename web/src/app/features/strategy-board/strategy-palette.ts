import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FFlowModule } from '@foblex/flow';
import { NzInputModule } from 'ng-zorro-antd/input';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { StrategyNodeType } from '../../core/api/models/strategies';
import { PALETTE, PALETTE_TABS, PaletteTab, unavailableReason } from './strategy-node-meta';

/** Pozycja palety: kafelek, który da się dodać, razem z powodem, dla którego akurat teraz się nie da. */
interface PaletteItem {
  readonly type: StrategyNodeType;
  readonly label: string;
  readonly unavailable: string | null;
}

/**
 * Panel „Dodaj kafelek” (makieta Figma „Strategia”: 377:97) — zakładki, wyszukiwarka i kafelki do przeciągnięcia
 * na tablicę albo dodania przyciskiem „+”.
 *
 * ⚠️ Element z `fExternalItem` jest poza `<f-flow>` w DOM, ale biblioteka łączy go z tablicą przez wstrzykiwanie —
 * dlatego komponent MUSI stać w szablonie komponentu, który ma `provideFFlow` w `providers`. Samo upuszczenie obsługuje
 * rodzic (`fCreateNode`); stąd tu tylko `add` dla przycisku „+”.
 */
@Component({
  selector: 'app-strategy-palette',
  imports: [FormsModule, FFlowModule, NzInputModule, TranslatePipe],
  templateUrl: './strategy-palette.html',
  styleUrl: './strategy-palette.scss',
})
export class StrategyPalette {
  private readonly translate = inject(TranslateService);

  /** Rodzaj zaznaczonego kafelka — od niego zależy, które pozycje są niedostępne. */
  readonly selectedType = input<StrategyNodeType | null>(null);

  readonly add = output<StrategyNodeType>();

  protected readonly tabs = PALETTE_TABS;
  protected readonly tab = signal<PaletteTab>('events');
  protected readonly search = signal('');

  protected readonly items = computed<readonly PaletteItem[]>(() => {
    const query = this.search().trim().toLocaleLowerCase();
    const selected = this.selectedType();
    return PALETTE[this.tab()]
      .map((type) => ({
        type,
        label: this.translate.instant(`strategies.board.types.${type}`) as string,
        unavailable: unavailableReason(selected, type),
      }))
      .filter((item) => !query || item.label.toLocaleLowerCase().includes(query));
  });

  protected hint(type: StrategyNodeType): string {
    return this.translate.instant(`strategies.board.hints.${type}`);
  }
}
