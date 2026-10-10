import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** Zakładki panelu bocznego tablicy: „Kafelki” (paleta i ustawienia kafelka) albo „Warianty”. */
export type PanelTab = 'tiles' | 'variants';

/**
 * Pasek zakładek panelu bocznego (makieta Figma „Strategia”: 423:451). Nic nie trzyma — wybór wraca do tablicy, bo to
 * ona decyduje, że zaznaczenie kafelka przełącza z powrotem na „Kafelki”.
 */
@Component({
  selector: 'app-strategy-panel-tabs',
  imports: [TranslatePipe],
  templateUrl: './strategy-panel-tabs.html',
  styleUrl: './strategy-panel-tabs.scss',
})
export class StrategyPanelTabs {
  readonly active = input<PanelTab>('tiles');

  readonly picked = output<PanelTab>();

  protected readonly tabs: readonly { readonly id: PanelTab; readonly label: string }[] = [
    { id: 'tiles', label: 'strategies.variants.tabTiles' },
    { id: 'variants', label: 'strategies.variants.tabVariants' },
  ];
}
