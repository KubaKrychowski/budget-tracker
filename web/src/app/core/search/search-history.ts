import { Injectable, signal } from '@angular/core';

/** Ile fraz pamiętamy. Dłuższa lista przestaje być „ostatnie” i zaczyna być archiwum. */
const MAX_ENTRIES = 6;

const STORAGE_KEY = 'bt.search.history';

/**
 * Historia wyszukiwań — wyłącznie w przeglądarce.
 *
 * ⚠️ Świadomie NIE w bazie (decyzja użytkownika). Frazy, których ktoś szukał we własnych finansach,
 * to dana wrażliwa; trzymanie ich na serwerze wymagałoby tabeli, migracji i odpowiedzi na pytanie,
 * po co je w ogóle przechowujemy. Cena: historia jest per przeglądarka i ginie z wyczyszczeniem danych.
 *
 * ⚠️ Każdy dostęp do `localStorage` jest w `try`. W prywatnym oknie i przy zablokowanych danych witryny
 * samo czytanie RZUCA, a wyszukiwarka ma wtedy działać bez historii, a nie wysypywać nagłówek aplikacji.
 */
@Injectable({ providedIn: 'root' })
export class SearchHistory {
  private readonly state = signal<readonly string[]>(this.read());

  /** Ostatnie frazy, najświeższa pierwsza. */
  readonly entries = this.state.asReadonly();

  /**
   * Dopisuje frazę na początek listy.
   *
   * Powtórzenie WĘDRUJE na górę zamiast się dublować — inaczej trzy próby tej samej frazy
   * wypchnęłyby z historii wszystko inne.
   */
  add(query: string): void {
    const value = query.trim();
    if (!value) return;

    const next = [value, ...this.state().filter((e) => e.toLowerCase() !== value.toLowerCase())]
      .slice(0, MAX_ENTRIES);

    this.state.set(next);
    this.write(next);
  }

  clear(): void {
    this.state.set([]);
    this.write([]);
  }

  private read(): readonly string[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return [];
      const parsed: unknown = JSON.parse(raw);
      // Zawartość storage to dane z ZEWNĄTRZ — mógł ją podmienić ktoś inny albo starsza wersja aplikacji.
      if (!Array.isArray(parsed)) return [];
      return parsed.filter((e): e is string => typeof e === 'string').slice(0, MAX_ENTRIES);
    } catch {
      return [];
    }
  }

  private write(entries: readonly string[]): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(entries));
    } catch {
      // Brak miejsca albo zablokowane dane witryny. Historia jest wygodą, nie funkcją krytyczną.
    }
  }
}
