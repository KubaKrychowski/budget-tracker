import { TestBed } from '@angular/core/testing';
import { RecentScreens } from './recent-screens';

/** Ostatnio odwiedzone ekrany — dopasowanie adresu do akcji i kolejność „najświeższy pierwszy". */
describe('RecentScreens', () => {
  let service: RecentScreens;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(RecentScreens);
  });

  const keys = (): string[] => service.screens().map((s) => s.key);

  it('odnotowuje ekran po adresie i stawia najświeższy na początku', () => {
    service.track('/limits');
    service.track('/transactions');

    expect(keys()).toEqual(['transaction-list', 'limits']);
  });

  it('powtórna wizyta WĘDRUJE na górę zamiast się dublować', () => {
    service.track('/limits');
    service.track('/transactions');
    service.track('/limits');

    expect(keys()).toEqual(['limits', 'transaction-list']);
  });

  it('parametry adresu nie tworzą osobnego wpisu', () => {
    service.track('/limits?month=2026-10-01&budgetId=b1');
    service.track('/limits');

    expect(keys()).toEqual(['limits']);
  });

  it('adres bez odpowiednika w rejestrze akcji jest pomijany', () => {
    // Dashboard nie jest miejscem, do którego się „wraca" — i nie ma swojej akcji.
    service.track('/dashboard');

    expect(keys()).toEqual([]);
  });

  it('pamięta najwyżej pięć ekranów — tyle, ile mieści rząd kafli', () => {
    ['/limits', '/transactions', '/savings', '/standing-orders', '/episodic-orders', '/import']
      .forEach((url) => service.track(url));

    expect(keys()).toHaveLength(5);
    expect(keys()[0]).toBe('import-statement');
    expect(keys()).not.toContain('limits');
  });

  it('przeżywa przeładowanie strony, bo siedzi w localStorage', () => {
    service.track('/limits');

    // Nowa instancja = to samo, co nowy start aplikacji w tej przeglądarce.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    expect(TestBed.inject(RecentScreens).screens().map((s) => s.key)).toEqual(['limits']);
  });

  it('uszkodzona zawartość storage nie wysadza aplikacji', () => {
    localStorage.setItem('bt.recent.screens', '{ to nie jest JSON');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    expect(TestBed.inject(RecentScreens).screens()).toEqual([]);
  });
});
