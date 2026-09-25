import { Injectable, signal } from '@angular/core';

/**
 * Prośba o otwarcie wyszukiwarki z innego miejsca niż ona sama — dziś z kafla „Wszystkie akcje".
 *
 * ⚠️ Licznik, nie flaga `boolean`. Druga prośba o to samo musi coś zmienić w sygnale, inaczej
 * `effect` w menu nie wystrzeli i drugie kliknięcie nie zrobi nic.
 */
@Injectable({ providedIn: 'root' })
export class SearchFocus {
  private readonly counter = signal(0);

  readonly requests = this.counter.asReadonly();

  open(): void {
    this.counter.update((n) => n + 1);
  }
}
