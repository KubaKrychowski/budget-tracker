import { describe, expect, it } from 'vitest';
import { HANDBOOK_TOPICS, handbookTopicByKey, handbookTopicKeyForRoute } from './handbook-topics';

describe('handbookTopicByKey', () => {
  it('zwraca temat o podanym kluczu', () => {
    expect(handbookTopicByKey('savings').file).toBe('handbook/savings.md');
  });

  it('nieznany albo pusty klucz wraca do pierwszego tematu z listy', () => {
    expect(handbookTopicByKey('cos-czego-nie-ma')).toBe(HANDBOOK_TOPICS[0]);
    expect(handbookTopicByKey(null)).toBe(HANDBOOK_TOPICS[0]);
    expect(handbookTopicByKey(undefined)).toBe(HANDBOOK_TOPICS[0]);
  });
});

describe('handbookTopicKeyForRoute', () => {
  it('mapuje trasę ekranu na jej temat podręcznika', () => {
    expect(handbookTopicKeyForRoute('/savings')).toBe('savings');
    expect(handbookTopicKeyForRoute('/standing-orders')).toBe('standing-orders');
  });

  it('ignoruje parametry zapytania przy dopasowaniu', () => {
    expect(handbookTopicKeyForRoute('/transactions?budgetId=abc&tab=review')).toBe('transactions');
  });

  it('rezerwacje mapują na ten sam temat co cele oszczędzania — to jeden ekran w podręczniku', () => {
    expect(handbookTopicKeyForRoute('/savings/reservations')).toBe('savings');
  });

  it('trasa spoza mapy (np. sam podręcznik) wraca do pierwszego tematu', () => {
    expect(handbookTopicKeyForRoute('/handbook')).toBe(HANDBOOK_TOPICS[0].key);
  });
});
