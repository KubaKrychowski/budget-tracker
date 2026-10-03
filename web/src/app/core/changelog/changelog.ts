/** Jedna pozycja wydania — tytuł, krótki opis i opcjonalny link do ekranu, którego dotyczy. */
export interface ChangelogItem {
  title: string;
  text: string;
  /** Trasa aplikacji (z ewentualnym `?query`), np. `/strategies`. */
  link?: string;
  linkLabel?: string;
}

/** Wydanie z `public/changelog.json`. Tablica jest uporządkowana od NAJNOWSZEGO i tylko ta kolejność mówi, co jest nowsze. */
export interface ChangelogRelease {
  id: string;
  /** Data w formacie `yyyy-MM-dd`. */
  date: string;
  pl: ChangelogItem[];
  en: ChangelogItem[];
}
