import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { NzModalService } from 'ng-zorro-antd/modal';
import { ChangelogRelease } from './changelog';
import { ChangelogDialog, ChangelogDialogData } from './changelog-dialog';

/**
 * „Co nowego" — które wydania użytkownik już widział. Lista wydań to statyczny `public/changelog.json`
 * (bez backendu), a ostatnio widziane wydanie zapamiętuje localStorage, więc stan jest osobny dla każdej przeglądarki.
 */
@Injectable({ providedIn: 'root' })
export class ChangelogService {
  /**
   * ⚠️ Prefiks `bt.` — wylogowanie i zmiana konta czyszczą takie klucze, więc następny użytkownik na tym
   * urządzeniu nie dziedziczy cudzego „przeczytane".
   */
  static readonly SeenKey = 'bt.changelog.seen';

  private readonly http = inject(HttpClient);
  private readonly modal = inject(NzModalService);

  readonly releases = signal<ChangelogRelease[]>([]);
  private readonly seen = signal<string | null>(readSeen());

  /**
   * Wydania nieprzeczytane, od najnowszego. Pierwsza wizyta (brak zapisu) albo zapis o nieznanym wydaniu
   * pokazuje tylko NAJNOWSZE — nowy użytkownik nie dostaje całej historii na dzień dobry.
   */
  readonly unseen = computed(() => {
    const all = this.releases();
    const seen = this.seen();
    const index = seen === null ? -1 : all.findIndex((r) => r.id === seen);
    return index < 0 ? all.slice(0, 1) : all.slice(0, index);
  });

  readonly hasUnread = computed(() => this.unseen().length > 0);

  /** Pobiera listę wydań; błąd (brak pliku, offline) zostawia ją pustą — „co nowego" nigdy nie blokuje aplikacji. */
  load(): void {
    this.http.get<ChangelogRelease[]>('changelog.json').subscribe({
      next: (releases) => this.releases.set(Array.isArray(releases) ? releases : []),
      error: () => this.releases.set([]),
    });
  }

  /**
   * Otwiera okno. `auto` — po aktualizacji (tylko nowe wydania), inaczej historia z ikony. Każde zamknięcie
   * (przycisk, Esc, kliknięcie w tło) oznacza wydania jako przeczytane, żeby okno nie wracało przy każdej wizycie.
   */
  open(auto: boolean): void {
    if (this.releases().length === 0) return;
    const data: ChangelogDialogData = {
      releases: this.releases(),
      newIds: this.unseen().map((r) => r.id),
      auto,
    };
    const ref = this.modal.create<ChangelogDialog, ChangelogDialogData>({
      nzContent: ChangelogDialog,
      nzData: data,
      nzFooter: null,
      nzClosable: false,
      nzWidth: 640,
    });
    ref.afterClose.subscribe(() => this.markSeen());
  }

  /** Oznacza najnowsze wydanie jako przeczytane. */
  markSeen(): void {
    const latest = this.releases()[0]?.id;
    if (!latest) return;
    this.seen.set(latest);
    try {
      localStorage.setItem(ChangelogService.SeenKey, latest);
    } catch {
      // localStorage niedostępny — „przeczytane" obowiązuje do końca sesji.
    }
  }
}

function readSeen(): string | null {
  try {
    return localStorage.getItem(ChangelogService.SeenKey);
  } catch {
    return null;
  }
}
