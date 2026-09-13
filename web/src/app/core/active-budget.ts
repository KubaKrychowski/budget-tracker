import { Injectable, signal } from '@angular/core';

/**
 * Budżet (albo budżety), który użytkownik ma właśnie w widoku — wspólny dla całej aplikacji.
 *
 * ⚠️ Powód istnienia (issue #16). Budżet między ekranami jeździł w adresie (`?budgetId`), ale
 * nie każde wejście go niosło: wyszukiwarka akcji w nagłówku, okruszki i linki wewnątrz ekranów
 * (oszczędności → rezerwacje, oszczędności → transakcje, rezerwacje → oszczędności) nawigowały
 * gołą trasą. A dashboard trzymał wybór wyłącznie w pamięci swojego komponentu. Skutek: wybierasz
 * budżet na dashboardzie, klikasz „Cele oszczędzania" — i ekran pokazuje budżet domyślny, bo nie
 * ma skąd wiedzieć, co wybrałeś. Przy kilku budżetach z tym samym miesiącem domyślny to ostatnio
 * UTWORZONY, więc świeży, pusty budżet przejmował każdy ekran.
 *
 * Reguła, którą to wprowadza: **adres wygrywa, gdy coś mówi; gdy milczy — obowiązuje budżet
 * z widoku.** Celowo nie dopisujemy `budgetId` do każdego linku: pięć miejsc już o tym zapomniało,
 * a szóste zapomniałoby przy następnym ekranie. Fallback w miejscu ODCZYTU nie da się pominąć.
 *
 * Tylko w pamięci karty, nie w `localStorage`: przeładowanie wraca do budżetu domyślnego tak
 * samo jak dotąd — ta zmiana naprawia rozjazd MIĘDZY ekranami w jednej wizycie, a nie to,
 * co ma się stać po przeładowaniu (to osobna decyzja o regule budżetu domyślnego).
 */
@Injectable({ providedIn: 'root' })
export class ActiveBudget {
  private readonly current = signal<readonly string[]>([]);

  /** Identyfikatory (BusinessId) budżetów w widoku; pusta lista = nic nie wiadomo, decyduje backend. */
  readonly ids = this.current.asReadonly();

  /** Ekran mówi, jakie budżety właśnie pokazuje. Kolejność i duplikaty nie mają znaczenia. */
  set(ids: readonly (string | null | undefined)[]): void {
    const next = [...new Set(ids.filter((id): id is string => !!id))];
    const prev = this.current();
    if (next.length === prev.length && next.every((id) => prev.includes(id))) return;
    this.current.set(next);
  }

  /**
   * Budżet przestał istnieć (usunięty w ustawieniach) — nie może zostać w widoku.
   *
   * ⚠️ Bez tego następny ekran wysłałby `budgetId` usuniętego budżetu, a API odpowiada na nieznany
   * identyfikator 404 (celowo — patrz `BudgetScope`), więc ekran pokazałby błąd zamiast danych.
   */
  forget(id: string): void {
    if (this.current().includes(id)) this.current.set(this.current().filter((x) => x !== id));
  }

  /** Budżety do zapytania: z adresu, jeśli adres je podaje; w przeciwnym razie te z widoku. */
  resolve(fromUrl: readonly string[]): readonly string[] {
    return fromUrl.length > 0 ? fromUrl : this.current();
  }
}
