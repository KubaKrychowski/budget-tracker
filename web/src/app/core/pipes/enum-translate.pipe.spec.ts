import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';
import { EnumTranslatePipe } from './enum-translate.pipe';

/**
 * Ten pipe nie jest jeszcze renderowany na żadnym ekranie (jedyne miejsce z wartością
 * enuma — lista ostatnich transakcji — zostało usunięte), więc test jest jego JEDYNĄ
 * weryfikacją. Nie kasuj go razem z ewentualnym refaktorem szablonów.
 */
describe('EnumTranslatePipe', () => {
  let pipe: EnumTranslatePipe;
  let translate: TranslateService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService(), EnumTranslatePipe],
    });

    translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      enums: {
        transactionStatus: { PendingReview: 'Do przeglądu' },
        accountType: { LunchCard: 'Karta lunchowa' },
      },
    });
    translate.use('pl');

    pipe = TestBed.inject(EnumTranslatePipe);
  });

  it('tłumaczy wartość enuma na tekst użytkownika', () => {
    expect(pipe.transform('PendingReview', 'transactionStatus')).toBe('Do przeglądu');
  });

  it('obsługuje każdy enum przez nazwę przekazaną w argumencie', () => {
    expect(pipe.transform('LunchCard', 'accountType')).toBe('Karta lunchowa');
  });

  it('wraca do surowej wartości, gdy brak tłumaczenia', () => {
    // Lepiej pokazać „Confirmed" niż „enums.transactionStatus.Confirmed".
    expect(pipe.transform('Confirmed', 'transactionStatus')).toBe('Confirmed');
  });

  it('zwraca pusty tekst dla braku wartości', () => {
    expect(pipe.transform(null, 'transactionStatus')).toBe('');
    expect(pipe.transform(undefined, 'transactionStatus')).toBe('');
  });

  it('przelicza się po zmianie języka', () => {
    translate.setTranslation('en', {
      enums: { transactionStatus: { PendingReview: 'Pending review' } },
    });
    translate.use('en');

    expect(pipe.transform('PendingReview', 'transactionStatus')).toBe('Pending review');
  });
});
