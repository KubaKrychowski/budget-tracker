/** Jeden wiersz podglądu — co system zrobiłby z tą pozycją, gdyby zapisać import. */
export interface PreviewRow {
  /** Numer porządkowy w podglądzie; wiersz nie ma jeszcze identyfikatora z bazy. */
  readonly index: number;
  readonly date: string;
  readonly amount: number;
  readonly description: string;
  readonly transactionType: string;
  readonly externalReference: string | null;
  /**
   * Podpowiedź kategorii — także wtedy, gdy pewność jest PONIŻEJ progu.
   * Obecność kategorii NIE znaczy „gotowe": od tego jest `needsReview`.
   * `null` tylko wtedy, gdy ani reguła, ani model nie mieli nic do powiedzenia.
   */
  readonly categoryId: string | null;
  readonly categoryName: string | null;
  readonly confidence: number | null;

  /** Czy wiersz czeka na decyzję człowieka. Liczy to serwer — to on zna próg. */
  readonly needsReview: boolean;
  /** Już jest w bazie — nie zostanie zapisany po raz drugi. */
  readonly duplicate: boolean;
  /**
   * Saldo rachunku po operacji, jak podał je bank; `null`, gdy pliku brak tej kolumny. Front tylko je przenosi:
   * serwer nie trzyma podglądu, więc zapis dostaje je z odesłanego wiersza.
   */
  readonly balanceAfter: number | null;
}
