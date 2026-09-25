import { Injectable, computed, signal } from '@angular/core';
import { SYSTEM_ACTIONS } from '../system-actions';
import { SystemAction } from '../models/system-action';

/** Ile ekranów pamiętamy — tyle, ile mieści się w jednym rzędzie kafli na dashboardzie. */
const MAX_ENTRIES = 5;

const STORAGE_KEY = 'bt.recent.screens';

/** Ekran domowy — patrz `track`, dlaczego nie trafia do historii. */
const HomeKey = 'dashboard';

/**
 * Ostatnio odwiedzone ekrany — zasilają kafle „Ostatnie akcje" na dashboardzie i sekcję
 * „Ostatnie ekrany" w menu wyszukiwarki.
 *
 * ⚠️ Liczymy ODWIEDZINY, nie wykonane akcje (decyzja użytkownika). Akcje wymagałyby dziennika zdarzeń,
 * którego aplikacja nie prowadzi, a kafle świeciłyby pustką u kogoś, kto głównie ogląda.
 *
 * ⚠️ Pamiętamy KLUCZE akcji, nie adresy. Zapisany adres rozjechałby się przy pierwszej zmianie trasy
 * i zostawił w storage kafel prowadzący donikąd; klucz zawsze da się przyłożyć do `SYSTEM_ACTIONS`,
 * a nieznany po prostu wypada.
 */
@Injectable({ providedIn: 'root' })
export class RecentScreens {
  private readonly state = signal<readonly string[]>(this.read());

  /** Ekrany od ostatnio odwiedzonego. Nieznane klucze odpadają przy odczycie. */
  readonly screens = computed<readonly SystemAction[]>(() => this.state()
    .map((key) => SYSTEM_ACTIONS.find((a) => a.key === key))
    .filter((a): a is SystemAction => a !== undefined));

  /**
   * Odnotowuje wejście na ekran. Adres bez odpowiednika w rejestrze akcji jest ignorowany.
   *
   * ⚠️ Dashboard jest wyjątkiem MIMO tego, że ma swoją akcję (katalog i wyszukiwarka muszą go
   * znać). Kafle „Ostatnie akcje" wiszą właśnie na dashboardzie, więc zapisywanie go znaczyłoby,
   * że pierwszym kaflem zawsze jest powrót tam, gdzie już jesteś.
   */
  track(url: string): void {
    const action = RecentScreens.match(url);
    if (!action || action.key === HomeKey) return;

    const next = [action.key, ...this.state().filter((k) => k !== action.key)].slice(0, MAX_ENTRIES);
    this.state.set(next);
    this.write(next);
  }

  clear(): void {
    this.state.set([]);
    this.write([]);
  }

  /**
   * Akcja odpowiadająca adresowi.
   *
   * Wygrywa NAJDŁUŻSZE dopasowanie: `/settings/rules` musi trafić w regułę, a nie w `/settings`,
   * bo krótsza trasa jest prefiksem dłuższej i przy zwykłym „pierwszy pasujący" zawsze by ją przykryła.
   */
  private static match(url: string): SystemAction | undefined {
    const path = url.split('?')[0].split('#')[0];
    return SYSTEM_ACTIONS
      .filter((a) => a.route !== null && (path === a.route || path.startsWith(a.route + '/')))
      .sort((a, b) => (b.route?.length ?? 0) - (a.route?.length ?? 0))[0];
  }

  private read(): readonly string[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return [];
      const parsed: unknown = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];
      return parsed.filter((e): e is string => typeof e === 'string').slice(0, MAX_ENTRIES);
    } catch {
      return [];
    }
  }

  private write(keys: readonly string[]): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(keys));
    } catch {
      // Prywatne okno albo zablokowane dane witryny — kafle po prostu nie przeżyją odświeżenia.
    }
  }
}
