import { TestBed } from '@angular/core/testing';
import { beforeAll, vi } from 'vitest';
import { StrategySpike } from './strategy-spike';

/**
 * Spike @foblex/flow: czy komponent z biblioteką w ogóle się montuje na Angularze 22 i renderuje
 * węzły, połączenia oraz kafelki palety. Geometrii (jsdom jej nie liczy) nie sprawdzamy — to
 * robi przegląd w przeglądarce.
 */
describe('StrategySpike', () => {
  /**
   * ⚠️ Biblioteka liczy rozmiar węzłów przez ResizeObserver, którego jsdom nie ma — bez stubu mount pada
   * z „ResizeObserver is not defined”. Przy właściwej implementacji stub ma trafić do src/test-setup.ts.
   */
  beforeAll(() => {
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe = vi.fn();
        unobserve = vi.fn();
        disconnect = vi.fn();
      },
    );
  });

  it('renderuje trzy węzły, dwa połączenia i trzy kafelki palety', async () => {
    const fixture = TestBed.createComponent(StrategySpike);
    fixture.detectChanges();
    await fixture.whenStable();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelectorAll('.f-node').length).toBe(3);
    expect(el.querySelectorAll('f-connection').length).toBe(2);
    expect(el.querySelectorAll('.f-external-item').length).toBe(3);
  });
});
