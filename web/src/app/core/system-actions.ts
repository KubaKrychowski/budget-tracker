import { SystemAction } from './models/system-action';

/**
 * Rejestr akcji, jakie system potrafi wykonać. Zasila wyszukiwarkę w nagłówku
 * (podpowiada „wszystkie możliwe opcje dostępne w systemie") oraz kafle Szybkich akcji.
 *
 * Etykiety to KLUCZE TŁUMACZEŃ, nie teksty — rozwiązuje je TranslateService.
 * Wartości siedzą w `public/i18n/*.json` pod `actions.*`.
 *
 * `duzy-wydatek-azure.svg` leży w `public/icons/` NIEUŻYWANY — dotyczy procesu 4
 * z Etapu 0 („oznacz duży wydatek"), który jest flagą na transakcji, nie akcją menu.
 */
export const SYSTEM_ACTIONS: readonly SystemAction[] = [
  {
    key: 'add-transaction',
    labelKey: 'actions.addTransaction',
    icon: 'plus-circle',
    svg: 'icons/dodaj-transakcje-azure.svg',
    route: null,
    quickAction: true,
  },
  {
    key: 'create-budget',
    labelKey: 'actions.createBudget',
    icon: 'wallet',
    svg: 'icons/utworz-budzet-azure.svg',
    route: '/create-budget',
    quickAction: true,
  },
  {
    key: 'transaction-list',
    labelKey: 'actions.transactionList',
    icon: 'unordered-list',
    svg: 'icons/lista-transakcji-azure.svg',
    route: '/transactions',
    quickAction: true,
  },
  {
    key: 'review-queue',
    labelKey: 'actions.reviewQueue',
    icon: 'exception',
    svg: 'icons/popraw-kategorie-azure.svg',
    route: null,
    quickAction: true,
  },
  {
    key: 'import-statement',
    labelKey: 'actions.importStatement',
    icon: 'upload',
    svg: 'icons/import-azure.svg',
    route: '/import',
    quickAction: false,
  },
  {
    key: 'savings',
    labelKey: 'actions.savings',
    // Własna ikona sekcji (tarcza). `icon` zostaje jako zapas, gdyby plik zniknął z `public/icons/`.
    icon: 'wallet',
    svg: 'icons/cele-oszczednosciowe-azure.svg',
    route: '/savings',
    quickAction: false,
  },
];

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
