import { fromIsoDate, toIsoDate } from '../../core/api/date-param';

const STORAGE_PREFIX = 'budget-tracker:dashboard-range:';

/**
 * Ostatnio wybrany zakres dat, zapamiętany OSOBNO per budżet — przełączenie budżetu
 * ma wrócić do okna, które user tam wcześniej ustawił, zamiast zawsze do domyślnych
 * 30 dni albo do okna zostawionego na poprzednim budżecie. Klucz to `BusinessId`
 * (Guid) — jedyny publiczny identyfikator budżetu (patrz CLAUDE.md §4).
 *
 * localStorage, nie backend: to wygoda UI TEJ przeglądarki, nie dana domenowa —
 * nie ma powodu trzymać jej w bazie ani synchronizować między urządzeniami.
 */
export function loadRememberedRange(budgetId: string): [Date, Date] | null {
  try {
    const raw = localStorage.getItem(STORAGE_PREFIX + budgetId);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as { from?: string; to?: string };
    if (!parsed.from || !parsed.to) return null;

    return [fromIsoDate(parsed.from), fromIsoDate(parsed.to)];
  } catch {
    // Prywatne okno, wyłączony storage, uszkodzony JSON — brak pamięci nie może
    // wywrócić dashboardu, więc traktujemy to tak samo jak „nic nie zapamiętano".
    return null;
  }
}

export function rememberRange(budgetId: string, range: [Date, Date]): void {
  try {
    localStorage.setItem(
      STORAGE_PREFIX + budgetId,
      JSON.stringify({ from: toIsoDate(range[0]), to: toIsoDate(range[1]) }),
    );
  } catch {
    // j.w. — zapis to najwyżej utracona wygoda, nie powód do wywrócenia ekranu.
  }
}
