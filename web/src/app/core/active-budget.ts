import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'budget-tracker:active-budget';

/**
 * Odczyt zapamiętanego wyboru z poprzedniej sesji — `null`/uszkodzony JSON/wyłączony storage
 * traktujemy tak samo jak „nic nie zapamiętano", zgodnie z konwencją `budget-range-memory.ts`.
 */
function loadRemembered(): readonly string[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((id): id is string => typeof id === 'string');
  } catch {
    return [];
  }
}

function remember(ids: readonly string[]): void {
  try {
    if (ids.length === 0) localStorage.removeItem(STORAGE_KEY);
    else localStorage.setItem(STORAGE_KEY, JSON.stringify(ids));
  } catch {
    // Prywatne okno, wyłączony storage — brak pamięci to najwyżej utracona wygoda.
  }
}

/**
 * Budżet (albo budżety), który użytkownik ma właśnie w widoku — wspólny dla całej aplikacji.
 *
 * ⚠️ Powód istnienia (issue #16, pierwotna wersja). Budżet między ekranami jeździł w adresie
 * (`?budgetId`), ale nie każde wejście go niosło: wyszukiwarka akcji w nagłówku, okruszki i linki
 * wewnątrz ekranów (oszczędności → rezerwacje, oszczędności → transakcje, rezerwacje → oszczędności)
 * nawigowały gołą trasą. A dashboard trzymał wybór wyłącznie w pamięci swojego komponentu. Skutek:
 * wybierasz budżet na dashboardzie, klikasz „Cele oszczędzania" — i ekran pokazuje budżet domyślny,
 * bo nie ma skąd wiedzieć, co wybrałeś. Przy kilku budżetach z tym samym miesiącem domyślny to
 * ostatnio UTWORZONY, więc świeży, pusty budżet przejmował każdy ekran.
 *
 * Reguła, którą to wprowadza: **adres wygrywa, gdy coś mówi; gdy milczy — obowiązuje budżet
 * z widoku.** Celowo nie dopisujemy `budgetId` do każdego linku: pięć miejsc już o tym zapomniało,
 * a szóste zapomniałoby przy następnym ekranie. Fallback w miejscu ODCZYTU nie da się pominąć.
 *
 * ⚠️ REWIZJA (issue #16, wersja druga). Wybór jeździł tylko w pamięci karty — przeładowanie
 * strony wracało do budżetu domyślnego, mimo że użytkownik był w trakcie oglądania czegoś innego.
 * Teraz `current` startuje z `localStorage` i każda zmiana tam wraca, więc przeładowanie i nowa
 * sesja przeglądarki widzą ten sam budżet co poprzednio. `localStorage`, nie backend — to wygoda
 * UI tej przeglądarki, nie dana domenowa (patrz `budget-range-memory.ts`).
 */
@Injectable({ providedIn: 'root' })
export class ActiveBudget {
  private readonly current = signal<readonly string[]>(loadRemembered());

  /** Identyfikatory (BusinessId) budżetów w widoku; pusta lista = nic nie wiadomo, decyduje backend. */
  readonly ids = this.current.asReadonly();

  /** Ekran mówi, jakie budżety właśnie pokazuje. Kolejność i duplikaty nie mają znaczenia. */
  set(ids: readonly (string | null | undefined)[]): void {
    const next = [...new Set(ids.filter((id): id is string => !!id))];
    const prev = this.current();
    if (next.length === prev.length && next.every((id) => prev.includes(id))) return;
    this.current.set(next);
    remember(next);
  }

  /**
   * Budżet przestał istnieć (usunięty w ustawieniach) — nie może zostać w widoku.
   *
   * ⚠️ Bez tego następny ekran wysłałby `budgetId` usuniętego budżetu, a API odpowiada na nieznany
   * identyfikator 404 (celowo — patrz `BudgetScope`), więc ekran pokazałby błąd zamiast danych.
   */
  forget(id: string): void {
    if (!this.current().includes(id)) return;
    const next = this.current().filter((x) => x !== id);
    this.current.set(next);
    remember(next);
  }

  /** Budżety do zapytania: z adresu, jeśli adres je podaje; w przeciwnym razie te z widoku. */
  resolve(fromUrl: readonly string[]): readonly string[] {
    return fromUrl.length > 0 ? fromUrl : this.current();
  }
}
