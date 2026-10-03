import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { NZ_MODAL_DATA, NzModalRef } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { ChangelogItem, ChangelogRelease } from './changelog';

/** Dane okna: wydania, które użytkownik dopiero zobaczy, i to, czy okno wyskoczyło samo (po aktualizacji). */
export interface ChangelogDialogData {
  releases: ChangelogRelease[];
  newIds: string[];
  /** `true` — okno po aktualizacji (tylko nowe wydania); `false` — historia otwarta z ikony w nagłówku. */
  auto: boolean;
}

/**
 * Okno „Co nowego". Po aktualizacji pokazuje tylko wydania nieprzeczytane (z linkiem „Pokaż wszystkie zmiany"),
 * z ikony w nagłówku od razu całą historię.
 */
@Component({
  selector: 'app-changelog-dialog',
  imports: [DatePipe, TranslatePipe, NzButtonModule],
  templateUrl: './changelog-dialog.html',
  styleUrl: './changelog-dialog.scss',
})
export class ChangelogDialog {
  private readonly data = inject<ChangelogDialogData>(NZ_MODAL_DATA);
  private readonly ref = inject(NzModalRef);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  protected readonly showAll = signal(!this.data.auto);
  protected readonly isNew = (id: string) => this.data.newIds.includes(id);

  protected readonly lang = computed<'pl' | 'en'>(() => (this.translate.currentLang() === 'en' ? 'en' : 'pl'));
  protected readonly locale = computed(() => this.lang());

  protected readonly visible = computed(() =>
    this.showAll() ? this.data.releases : this.data.releases.filter((r) => this.isNew(r.id)),
  );

  /** Liczba wydań w podtytule, odmieniona regułami języka (1 wydanie / 2 wydania / 5 wydań). */
  protected readonly countKey = computed(() => {
    const category = new Intl.PluralRules(this.lang()).select(this.data.newIds.length);
    return `changelog.sinceVisit.${category}`;
  });

  protected readonly count = this.data.newIds.length;
  protected readonly hasNew = this.data.newIds.length > 0;

  protected items(release: ChangelogRelease): ChangelogItem[] {
    return release[this.lang()] ?? release.pl;
  }

  protected showHistory(): void {
    this.showAll.set(true);
  }

  protected close(): void {
    this.ref.close();
  }

  /** Link do ekranu, którego dotyczy wpis — zamyka okno i przechodzi (adres może mieć `?query`). */
  protected go(event: Event, link: string): void {
    event.preventDefault();
    this.ref.close();
    void this.router.navigateByUrl(link);
  }
}
