import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Pasek „strzałka wstecz + tytuł strony" na górze ekranów podrzędnych (import,
 * ustawienia, tworzenie budżetu). Wydzielony, bo żył w trzech miejscach z BAJT
 * W BAJT identycznym CSS i różniącą się tylko prefiksem klasy — dokładnie ten
 * przypadek, o którym CLAUDE.md §10 mówi „duplikat w 3. slice, wyciągnij".
 *
 * To NIE jest `app-header` (zielony pasek z logo/wyszukiwarką/zębatką) — ten jest
 * już globalny, w `App`, i renderuje się nad `<router-outlet>` niezależnie od trasy.
 * Ten komponent to DRUGI, lokalny pasek, który każda podstrona miała swój własny.
 */
@Component({
  selector: 'app-page-header',
  imports: [RouterLink, NzIconModule, TranslatePipe],
  templateUrl: './page-header.html',
  styleUrl: './page-header.scss',
})
export class PageHeader {
  /** Tytuł strony — WOŁAJĄCY tłumaczy klucz i przekazuje gotowy tekst. */
  readonly title = input.required<string>();

  /** Dokąd prowadzi strzałka. Każde dotychczasowe użycie wraca na dashboard. */
  readonly backTo = input('/dashboard');
}
