import { TestBed } from '@angular/core/testing';
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { ActiveBudget } from './active-budget';

const STORAGE_KEY = 'budget-tracker:active-budget';

/**
 * Budżet w widoku — reguła „adres wygrywa, gdy coś mówi; gdy milczy, obowiązuje budżet z widoku".
 */
describe('ActiveBudget', () => {
  let active: ActiveBudget;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    active = TestBed.inject(ActiveBudget);
  });

  afterEach(() => localStorage.clear());

  it('bez niczego w widoku i w adresie zostawia decyzję backendowi (pusta lista)', () => {
    expect(active.resolve([])).toEqual([]);
  });

  it('gdy adres milczy, zwraca budżet z widoku', () => {
    // Sedno #16: wejście z nagłówka albo okruszków nie niesie budgetId, a ekran ma pokazać
    // ten budżet, który użytkownik właśnie oglądał — nie domyślny.
    active.set(['domowy']);
    expect(active.resolve([])).toEqual(['domowy']);
  });

  it('gdy adres podaje budżet, adres wygrywa z budżetem z widoku', () => {
    // Zakładki i udostępnione linki mają działać jak dotąd, niezależnie od tego, co było w widoku.
    active.set(['domowy']);
    expect(active.resolve(['wariant-b'])).toEqual(['wariant-b']);
  });

  it('ignoruje puste identyfikatory i duplikaty', () => {
    active.set(['domowy', null, undefined, '', 'domowy']);
    expect(active.ids()).toEqual(['domowy']);
  });

  it('usunięty budżet znika z widoku, a pozostałe zostają', () => {
    // ⚠️ Bez tego następny ekran wysłałby identyfikator usuniętego budżetu i dostał 404.
    active.set(['domowy', 'wariant-b']);
    active.forget('wariant-b');
    expect(active.ids()).toEqual(['domowy']);
  });

  it('zapomnienie budżetu, którego nie ma w widoku, niczego nie rusza', () => {
    active.set(['domowy']);
    active.forget('inny');
    expect(active.ids()).toEqual(['domowy']);
  });

  it('#16: przy starcie odczytuje wybór zapamiętany w localStorage z poprzedniej sesji', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(['domowy']));
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const restarted = TestBed.inject(ActiveBudget);
    expect(restarted.resolve([])).toEqual(['domowy']);
  });

  it('#16: uszkodzony wpis w localStorage traktuje jak brak zapamiętanego wyboru', () => {
    localStorage.setItem(STORAGE_KEY, '{nie json');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const restarted = TestBed.inject(ActiveBudget);
    expect(restarted.resolve([])).toEqual([]);
  });

  it('#16: zmiana budżetu w widoku zapisuje wybór do localStorage', () => {
    active.set(['domowy']);
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!)).toEqual(['domowy']);
  });

  it('#16: zapomnienie ostatniego budżetu z widoku czyści localStorage', () => {
    active.set(['domowy']);
    active.forget('domowy');
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });
});
