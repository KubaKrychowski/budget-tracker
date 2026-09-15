import { Injectable, signal } from '@angular/core';

/**
 * Czy panel terminala (issue #25) jest otwarty — jeden stan dla całej aplikacji, bo ikona
 * w nagłówku i sam panel `<app-terminal>` żyją w różnych miejscach szablonu (`App`).
 */
@Injectable({ providedIn: 'root' })
export class TerminalService {
  private readonly openState = signal(false);
  readonly isOpen = this.openState.asReadonly();

  open(): void {
    this.openState.set(true);
  }

  close(): void {
    this.openState.set(false);
  }

  toggle(): void {
    this.openState.update((open) => !open);
  }
}
