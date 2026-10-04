import { Injectable, signal } from '@angular/core';

/**
 * Granica widoku mobilnego — ta sama co w `@media (max-width: 768px)` w stylach ekranów. Poniżej niej powłoka
 * pokazuje dolny pasek nawigacji, a ekrany przełączają tabele na karty (makiety Figma, strona „Mobile (propozycja)”).
 */
export const MOBILE_MEDIA_QUERY = '(max-width: 768px)';

/** Czy ekran jest wąski jak telefon — sygnał, bo część ekranów renderuje wtedy INNY szablon, nie tylko inne CSS. */
@Injectable({ providedIn: 'root' })
export class Viewport {
  // `matchMedia` bywa niedostępne (jsdom w testach) — wtedy widok desktopowy, jak przed wersją mobilną.
  private readonly query = typeof window.matchMedia === 'function' ? window.matchMedia(MOBILE_MEDIA_QUERY) : null;

  private readonly mobile = signal(this.query?.matches ?? false);
  readonly isMobile = this.mobile.asReadonly();

  constructor() {
    this.query?.addEventListener('change', (event) => this.mobile.set(event.matches));
  }
}
