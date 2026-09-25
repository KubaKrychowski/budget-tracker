import { SystemAction } from './models/system-action';

/**
 * Rejestr akcji, jakie system potrafi wykonać. Zasila wyszukiwarkę w nagłówku
 * (podpowiada „wszystkie możliwe opcje dostępne w systemie") oraz kafle Szybkich akcji.
 *
 * Etykiety to KLUCZE TŁUMACZEŃ, nie teksty — rozwiązuje je TranslateService.
 * Wartości siedzą w `public/i18n/*.json` pod `actions.*`.
 *
 * `duzy-wydatek-azure.svg` leży w `public/icons/` NIEUŻYWANY — flaga „duży wydatek” zniknęła,
 * zastąpiły ją zlecenia epizodyczne z własną ikoną.
 */
export const SYSTEM_ACTIONS: readonly SystemAction[] = [
  {
    key: 'add-transaction',
    labelKey: 'actions.addTransaction',
    icon: 'plus-circle',
    svg: 'icons/dodaj-transakcje-azure.svg',
    route: null,
    quickAction: true,
    group: 'daily',
  },
  {
    key: 'create-budget',
    labelKey: 'actions.createBudget',
    icon: 'wallet',
    svg: 'icons/utworz-budzet-azure.svg',
    route: '/create-budget',
    quickAction: true,
    group: 'budgets',
  },
  {
    key: 'transaction-list',
    labelKey: 'actions.transactionList',
    icon: 'unordered-list',
    svg: 'icons/lista-transakcji-azure.svg',
    route: '/transactions',
    quickAction: true,
    group: 'daily',
  },
  {
    key: 'review-queue',
    labelKey: 'actions.reviewQueue',
    icon: 'exception',
    svg: 'icons/popraw-kategorie-azure.svg',
    route: null,
    quickAction: true,
    group: 'daily',
  },
  {
    key: 'import-statement',
    labelKey: 'actions.importStatement',
    icon: 'upload',
    svg: 'icons/import-azure.svg',
    route: '/import',
    quickAction: false,
    group: 'daily',
  },
  {
    key: 'savings',
    labelKey: 'actions.savings',
    // Własna ikona sekcji (tarcza). `icon` zostaje jako zapas, gdyby plik zniknął z `public/icons/`.
    icon: 'wallet',
    svg: 'icons/cele-oszczednosciowe-azure.svg',
    route: '/savings',
    quickAction: false,
    group: 'planning',
  },
  {
    key: 'limits',
    labelKey: 'actions.limits',
    // Własna ikona sekcji (wskaźnik). `icon` zostaje jako zapas, gdyby plik zniknął z `public/icons/`.
    icon: 'pie-chart',
    svg: 'icons/limity-wydatkow-azure.svg',
    route: '/limits',
    quickAction: false,
    group: 'planning',
  },
  {
    key: 'standing-orders',
    labelKey: 'actions.standingOrders',
    // Własna ikona sekcji. `icon` zostaje jako zapas, gdyby plik zniknął z `public/icons/`.
    icon: 'sync',
    svg: 'icons/zlecenia-stale-azure.svg',
    route: '/standing-orders',
    quickAction: false,
    group: 'planning',
  },
  {
    key: 'episodic-orders',
    labelKey: 'actions.episodicOrders',
    // Własna ikona sekcji. `icon` zostaje jako zapas, gdyby plik zniknął z `public/icons/`.
    icon: 'unordered-list',
    svg: 'icons/wydatki-epizodyczne-azure.svg',
    route: '/episodic-orders',
    quickAction: false,
    group: 'planning',
  },
  // ── Ekrany spoza kafli: nie sa „szybka akcja", ale katalog i wyszukiwarka maja je znac ──
  {
    key: 'dashboard',
    labelKey: 'actions.dashboard',
    icon: 'pie-chart',
    svg: 'icons/dashboard-azure.svg',
    route: '/dashboard',
    quickAction: false,
    group: 'budgets',
  },
  {
    key: 'reservations',
    labelKey: 'actions.reservations',
    icon: 'wallet',
    svg: 'icons/cele-oszczednosciowe-azure.svg',
    route: '/savings/reservations',
    quickAction: false,
    group: 'planning',
  },
  {
    key: 'category-rules',
    labelKey: 'actions.categoryRules',
    icon: 'exception',
    svg: 'icons/popraw-kategorie-azure.svg',
    route: '/settings',
    queryParams: { tab: 'rules' },
    quickAction: false,
    group: 'settings',
  },
  {
    key: 'model-training',
    labelKey: 'actions.modelTraining',
    icon: 'pie-chart',
    svg: 'icons/limity-wydatkow-azure.svg',
    route: '/settings',
    queryParams: { tab: 'training' },
    quickAction: false,
    group: 'settings',
  },
  {
    key: 'all-functions',
    labelKey: 'actions.allFunctions',
    icon: 'unordered-list',
    svg: 'icons/dashboard-azure.svg',
    route: '/functions',
    quickAction: false,
    group: 'settings',
  },
  {
    key: 'handbook',
    labelKey: 'actions.handbook',
    icon: 'unordered-list',
    svg: 'icons/lista-transakcji-azure.svg',
    route: '/handbook',
    quickAction: false,
    group: 'settings',
  },
];

/**
 * Kolejnosc sekcji w katalogu „Wszystkie funkcje". Od tego, co robi sie najczesciej,
 * do tego, co ustawia sie raz.
 */
export const FUNCTION_GROUPS = ['daily', 'planning', 'budgets', 'settings'] as const;

/**
 * Akcje odnajdujemy po kluczu, nigdy po indeksie tablicy. Wcześniej CTA pustego
 * ekranu wołało `quickActions[0]`, co po cichu uruchamiało „Dodaj transakcje"
 * zamiast importu — literówka nie do wychwycenia przez kompilator.
 */
export function actionByKey(key: string): SystemAction {
  const action = SYSTEM_ACTIONS.find((a) => a.key === key);
  if (!action) {
    throw new Error(`Nieznana akcja systemu: "${key}". Dodaj ją do SYSTEM_ACTIONS.`);
  }
  return action;
}

export const QUICK_ACTIONS: readonly SystemAction[] = SYSTEM_ACTIONS.filter((a) => a.quickAction);
