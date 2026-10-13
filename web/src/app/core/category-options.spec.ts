import { CategoryOption } from './api/models/category-option';
import { pickerOptions } from './category-options';

describe('pickerOptions', () => {
  const visible: CategoryOption = { id: 'a', name: 'Jedzenie', type: 'Expense' };
  const hidden: CategoryOption = { id: 'b', name: 'Paliwo', type: 'Expense', hidden: true };

  it('pomija ukryte kategorie', () => {
    expect(pickerOptions([visible, hidden], null)).toEqual([visible]);
  });

  it('zostawia ukrytą kategorię, jeśli jest aktualnie wybrana — inaczej select byłby pusty', () => {
    // Łapie puste pole przy transakcji w ukrytej kategorii i ciche skasowanie kategorii przy zapisie.
    expect(pickerOptions([visible, hidden], 'b')).toEqual([visible, hidden]);
  });

  it('kategoria bez flagi traktowana jest jak widoczna', () => {
    expect(pickerOptions([visible], undefined)).toEqual([visible]);
  });
});
