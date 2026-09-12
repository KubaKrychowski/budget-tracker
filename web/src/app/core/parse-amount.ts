/**
 * Zamienia zawartość pola kwotowego na liczbę — w formatach, w jakich kwoty NAPRAWDĘ
 * trafiają do tej aplikacji: z wyciągu bankowego, z Excela i ze skopiowania ze strony banku.
 * Podawana do `nz-input-number` jako `[nzParser]`.
 *
 * ⚠️ Domyślny parser NG-ZORRO zakłada format en-US: usuwa PRZECINKI (jako separator tysięcy)
 * i zostawia spacje. Na polskich kwotach daje to dwa różne błędy, z czego drugi jest groźniejszy:
 * - `6 278,88` → `6 27888` → `NaN`, więc wklejenie jest odrzucane i pole wraca do poprzedniej
 *   wartości (na ekranie wygląda to jak wyzerowanie),
 * - `278,88` → `27888`, czyli **kwota stukrotnie za duża, bez żadnego objawu**. To gorszy
 *   przypadek: nic nie wygląda na zepsute, a do bazy idzie inna liczba niż ta na wyciągu.
 *
 * Zwraca `NaN` dla tekstu, z którego nie da się wyczytać liczby — tak NG-ZORRO rozpoznaje
 * „nie ruszaj wartości", więc kontrakt jest zachowany.
 */
export function parseAmount(value: string): number {
  // ⚠️ `\s` w JS obejmuje twardą spację (U+00A0) i wąską twardą spację (U+202F), a to
  // WŁAŚNIE one przychodzą przy kopiowaniu z Excela i ze stron banków — zwykła spacja
  // jest tam rzadsza. Dlatego nie trzeba ich wymieniać po kodach, ale trzeba wiedzieć,
  // że są objęte; zamiana `\s` na `[ ]` cicho zepsułaby najczęstszy przypadek.
  const trimmed = value.trim();

  // Format księgowy Excela zapisuje minus nawiasami. Nawiasy i tak wypadłyby niżej razem
  // z walutą, więc bez tej linii `(1 234,56)` zmieniłoby ZNAK kwoty, nie tylko format.
  const parenthesized = /^\(.*\)$/.test(trimmed);

  const cleaned = trimmed
    .replace(/\s/g, '')
    .replace(/[−–—]/g, '-') // minus typograficzny, półpauza, pauza
    .replace(/[^\d.,+-]/g, ''); // „zł", „PLN", nawiasy, wszystko inne

  if (!/\d/.test(cleaned)) return NaN;

  const negative = parenthesized || cleaned.startsWith('-') || cleaned.endsWith('-');
  const digits = cleaned.replace(/[+-]/g, '');

  const separator = decimalSeparatorOf(digits);
  const normalized =
    separator === null
      ? digits.replace(/[.,]/g, '')
      : digits.replace(separator === ',' ? /\./g : /,/g, '').replace(separator, '.');

  const parsed = Number(normalized);
  if (Number.isNaN(parsed)) return NaN;

  return negative ? -parsed : parsed;
}

/**
 * Który znak jest separatorem dziesiętnym — albo `null`, gdy wszystkie są separatorami tysięcy.
 *
 * ⚠️ Tu mieszka jedyna niejednoznaczność tego parsera i dlatego jest rozstrzygnięta jawnie,
 * a nie zostawiona przypadkowi: `6.278` może znaczyć „sześć tysięcy" (format polski) albo
 * „sześć i grosze" (format angielski). Przy grupie DOKŁADNIE trzech cyfr czytamy to jako
 * tysiące, bo aplikacja jest `pl` i kwoty pochodzą z polskich wyciągów. `6.50` (dwie cyfry)
 * zostaje kwotą dziesiętną, więc wpisywanie z klawiatury numerycznej dalej działa.
 *
 * Wymóg „część całkowita ma 1–3 cyfry i nie zaczyna się od zera" odcina `0.500`: zero przed
 * separatorem tysięcy nie istnieje w żadnym zapisie grupowanym, więc to na pewno jest 0,5.
 */
function decimalSeparatorOf(digits: string): '.' | ',' | null {
  const commas = (digits.match(/,/g) ?? []).length;
  const dots = (digits.match(/\./g) ?? []).length;

  // Oba znaki obecne: ten dalej z prawej jest dziesiętnym, drugi grupuje tysiące.
  if (commas > 0 && dots > 0) return digits.lastIndexOf(',') > digits.lastIndexOf('.') ? ',' : '.';

  // Powtórzony separator nie może być dziesiętny — `1.234.567` to zawsze tysiące.
  if (commas > 1 || dots > 1) return null;

  // Przecinek w formacie polskim jest dziesiętny zawsze, także przed trzema cyframi:
  // `1,234` to jeden i 234 tysięczne, nie tysiąc dwieście.
  if (commas === 1) return ',';
  if (dots === 0) return null;

  return /^[1-9]\d{0,2}\.\d{3}$/.test(digits) ? null : '.';
}
