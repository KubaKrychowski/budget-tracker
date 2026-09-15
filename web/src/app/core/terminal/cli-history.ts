const STORAGE_KEY = 'budget-tracker:terminal-history';
const MAX_ENTRIES = 100;

/**
 * Historia poleceń terminala (strzałki góra/dół) — `sessionStorage`, nie `localStorage`: to pamięć
 * TEJ karty na czas TEJ wizyty, jak schowek, a nie coś, co ma przetrwać zamknięcie przeglądarki
 * (w odróżnieniu od `active-budget.ts`, gdzie wybór budżetu ma przetrwać). Wzorzec zapisu/odczytu
 * w `try/catch` ten sam co `dashboard/budget-range-memory.ts`.
 */
export function loadHistory(): readonly string[] {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed.filter((v): v is string => typeof v === 'string') : [];
  } catch {
    return [];
  }
}

/** Dopisuje linię i zwraca nową historię — wołający trzyma ją u siebie, żeby nie czytać sessionStorage co znak. */
export function appendHistory(line: string): readonly string[] {
  const next = [...loadHistory(), line].slice(-MAX_ENTRIES);
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Prywatne okno, wyłączony storage — brak historii to najwyżej utracona wygoda.
  }
  return next;
}
