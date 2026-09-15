/** Jeden temat podręcznika: pozycja w liście po lewej + plik z treścią. */
export interface HandbookTopic {
  readonly key: string;
  readonly labelKey: string;
  readonly file: string;
}

/**
 * Rejestr tematów podręcznika (issue #19). Kolejność = kolejność w liście po lewej.
 * Treść leży w `public/handbook/<key>.md` — zwykłe pliki statyczne, renderowane przez
 * `ngx-markdown` (`<markdown [src]="...">`), tym samym trybem co `public/i18n/*.json`
 * dla tekstów UI: treść dla użytkownika nie siedzi w kodzie.
 */
export const HANDBOOK_TOPICS: readonly HandbookTopic[] = [
  { key: 'dashboard', labelKey: 'handbook.topics.dashboard', file: 'handbook/dashboard.md' },
  { key: 'budgets', labelKey: 'handbook.topics.budgets', file: 'handbook/budgets.md' },
  { key: 'import', labelKey: 'handbook.topics.import', file: 'handbook/import.md' },
  { key: 'transactions', labelKey: 'handbook.topics.transactions', file: 'handbook/transactions.md' },
  { key: 'limits', labelKey: 'handbook.topics.limits', file: 'handbook/limits.md' },
  { key: 'standing-orders', labelKey: 'handbook.topics.standingOrders', file: 'handbook/standing-orders.md' },
  { key: 'episodic-orders', labelKey: 'handbook.topics.episodicOrders', file: 'handbook/episodic-orders.md' },
  { key: 'savings', labelKey: 'handbook.topics.savings', file: 'handbook/savings.md' },
  { key: 'settings', labelKey: 'handbook.topics.settings', file: 'handbook/settings.md' },
];

/** Nieznany/pusty klucz wraca do pierwszego tematu, zamiast pokazać pusty ekran. */
export function handbookTopicByKey(key: string | null | undefined): HandbookTopic {
  return HANDBOOK_TOPICS.find((t) => t.key === key) ?? HANDBOOK_TOPICS[0];
}

/**
 * Mapowanie trasy na temat — używane przez ikonę w nagłówku, żeby otwierała podręcznik
 * OD RAZU na temacie ekranu, z którego użytkownik przyszedł (issue #19).
 */
const ROUTE_TOPIC: Readonly<Record<string, string>> = {
  '/dashboard': 'dashboard',
  '/create-budget': 'budgets',
  '/import': 'import',
  '/transactions': 'transactions',
  '/limits': 'limits',
  '/standing-orders': 'standing-orders',
  '/episodic-orders': 'episodic-orders',
  '/savings': 'savings',
  '/savings/reservations': 'savings',
  '/settings': 'settings',
};

/** Trasa spoza mapy (np. sam `/handbook`) wraca do pierwszego tematu jak `handbookTopicByKey`. */
export function handbookTopicKeyForRoute(url: string): string {
  const path = url.split('?')[0].split('#')[0];
  return ROUTE_TOPIC[path] ?? HANDBOOK_TOPICS[0].key;
}
