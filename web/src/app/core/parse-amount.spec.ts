import { describe, expect, it } from 'vitest';

import { parseAmount } from './parse-amount';

describe('parseAmount', () => {
  it('czyta kwotę z wyciągu: spacja jako tysiące, przecinek jako grosze', () => {
    // Zgłoszony przypadek. Domyślny parser NG-ZORRO robił z tego NaN, więc wklejenie
    // przepadało i pole wracało do poprzedniej wartości.
    expect(parseAmount('6 278,88')).toBe(6278.88);
  });

  it('czyta twardą i wąską twardą spację, bo to one przychodzą z Excela i ze stron banków', () => {
    // ⚠️ Najczęstszy realny wariant wklejenia — zwykła spacja jest tu rzadsza niż U+00A0.
    expect(parseAmount('6 278,88')).toBe(6278.88);
    expect(parseAmount('6 278,88')).toBe(6278.88);
    expect(parseAmount('1 234 567,89')).toBe(1234567.89);
  });

  it('nie zawyża kwoty stukrotnie przy samym przecinku dziesiętnym', () => {
    // ⚠️ To był groźniejszy błąd niż zgłoszony: domyślny parser usuwał przecinek jako
    // separator tysięcy, więc „278,88" zapisywało się jako 27888 i NIC nie wyglądało na zepsute.
    expect(parseAmount('278,88')).toBe(278.88);
    expect(parseAmount('0,01')).toBe(0.01);
    expect(parseAmount('1,5')).toBe(1.5);
  });

  it('przyjmuje kropkę dziesiętną, żeby wpisywanie z klawiatury numerycznej dalej działało', () => {
    expect(parseAmount('6278.88')).toBe(6278.88);
    expect(parseAmount('6.50')).toBe(6.5);
    expect(parseAmount('0.5')).toBe(0.5);
  });

  it('czyta format z kropką jako separatorem tysięcy i przecinkiem dziesiętnym', () => {
    expect(parseAmount('6.278,88')).toBe(6278.88);
    expect(parseAmount('1.234.567,89')).toBe(1234567.89);
  });

  it('czyta format angielski, gdy przecinek stoi przed kropką', () => {
    // Rozstrzyga POZYCJA, nie założony język: dalej z prawej = separator dziesiętny.
    expect(parseAmount('6,278.88')).toBe(6278.88);
    expect(parseAmount('1,234,567.89')).toBe(1234567.89);
  });

  it('traktuje samotną kropkę przed trzema cyframi jako tysiące, a przecinek zawsze jako grosze', () => {
    // Jedyna niejednoznaczność parsera, rozstrzygnięta na korzyść formatu polskiego —
    // patrz doc przy decimalSeparatorOf.
    expect(parseAmount('6.278')).toBe(6278);
    expect(parseAmount('1,234')).toBe(1.234);
  });

  it('nie bierze zera przed separatorem za grupowanie tysięcy', () => {
    // `0.500` nie może być „pięćset" — grupowanie nigdy nie zaczyna się od zera.
    expect(parseAmount('0.500')).toBe(0.5);
  });

  it('odrzuca resztki formatowania: walutę, znak plus, białe znaki na brzegach', () => {
    expect(parseAmount('6 278,88 zł')).toBe(6278.88);
    expect(parseAmount('6 278,88 PLN')).toBe(6278.88);
    expect(parseAmount('  6 278,88  ')).toBe(6278.88);
    expect(parseAmount('+6 278,88')).toBe(6278.88);
  });

  it('zachowuje znak ujemny, także w zapisach, które nie używają zwykłego minusa', () => {
    // ⚠️ Błąd znaku jest w kwotach tak samo cichy jak błąd rzędu wielkości: wydatek
    // zapisany jako przychód nie wygląda na usterkę, tylko na inne dane.
    expect(parseAmount('-6 278,88')).toBe(-6278.88);
    expect(parseAmount('−6 278,88')).toBe(-6278.88); // minus matematyczny U+2212
    expect(parseAmount('6 278,88-')).toBe(-6278.88); // minus na końcu (część eksportów)
    expect(parseAmount('(6 278,88)')).toBe(-6278.88); // format księgowy Excela
  });

  it('zwraca NaN, gdy nie ma z czego wyczytać liczby', () => {
    // NG-ZORRO rozpoznaje po NaN, że ma NIE ruszać wartości pola — zwrócenie tu 0
    // zamieniłoby literówkę w cichy zapis zera.
    expect(parseAmount('')).toBeNaN();
    expect(parseAmount('   ')).toBeNaN();
    expect(parseAmount('abc')).toBeNaN();
    expect(parseAmount('zł')).toBeNaN();
    expect(parseAmount('-')).toBeNaN();
  });
});
