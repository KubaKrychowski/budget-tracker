/**
 * Kolory dla ApexCharts.
 *
 * To JEDYNE miejsce w kodzie TS, gdzie wolno trzymać hexy. Powód: motyw NG-ZORRO
 * kompiluje się z Lessa i nie wystawia zmiennych CSS, a ApexCharts przyjmuje kolory
 * jako stringi w konfiguracji JS — nie da się ich podać selektorem.
 *
 * Wartości są kopią z `web/src/styles/design-tokens.less` (źródło: Figma Design System).
 * Zmiana koloru w design systemie wymaga aktualizacji OBU plików.
 */
export const CHART_COLORS = {
  primary500: '#51C273',
  primary400: '#5BD880',
  primary200: '#A4F9B7',
  error500: '#E44E44',
  warn500: '#DA9B33',
  info500: '#3E9FEA',
  secondary500: '#8075CD',
  neutral200: '#D9E8DD',
  neutral500: '#78877C',
  neutral900: '#252B27',
} as const;

/** Kolejność kolorów dla serii kategorii — cyklicznie, gdy kategorii jest więcej niż barw. */
export const CATEGORY_SERIES_COLORS = [
  CHART_COLORS.primary500,
  CHART_COLORS.info500,
  CHART_COLORS.secondary500,
  CHART_COLORS.warn500,
  CHART_COLORS.error500,
  CHART_COLORS.primary200,
];
