import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { MarkdownComponent } from 'ngx-markdown';
import { PageHeader } from '../../core/page-header/page-header';
import { HANDBOOK_TOPICS, handbookTopicByKey } from '../../core/handbook-topics';
import { normalizeText } from '../../core/normalize-text';

/**
 * Podręcznik użytkownika (issue #19) — lista tematów po lewej, treść z `public/handbook/*.md`
 * po prawej. Wybrany temat siedzi w adresie (`?topic=`), tak jak wybór budżetu na innych
 * ekranach: da się zapisać w zakładkach i wraca po cofnięciu w przeglądarce.
 */
@Component({
  selector: 'app-handbook',
  imports: [
    RouterLink, FormsModule, TranslatePipe, NzBreadCrumbModule, NzInputModule, NzIconModule,
    MarkdownComponent, PageHeader,
  ],
  templateUrl: './handbook.html',
  styleUrl: './handbook.scss',
})
export class Handbook {
  private readonly route = inject(ActivatedRoute);
  private readonly http = inject(HttpClient);
  private readonly translate = inject(TranslateService);

  protected readonly topics = HANDBOOK_TOPICS;
  protected readonly query = signal('');

  private readonly queryTopic = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  protected readonly selected = computed(() => handbookTopicByKey(this.queryTopic().get('topic')));

  /**
   * Treść WSZYSTKICH tematów naraz — wyłącznie do wyszukiwania; ekran nadal renderuje jeden
   * wybrany plik przez `<markdown [src]>`. 9 małych plików tekstowych ładuje się od razu przy
   * wejściu na ekran zamiast dogrywać je pojedynczo przy każdym wpisanym znaku. Nieudane
   * pobranie (np. usunięty plik) nie wywraca wyszukiwania — po prostu ten temat nie pasuje
   * po treści, tylko po tytule.
   */
  private readonly topicContents = toSignal(
    forkJoin(
      Object.fromEntries(
        HANDBOOK_TOPICS.map((t) => [
          t.key,
          this.http.get(t.file, { responseType: 'text' }).pipe(catchError(() => of(''))),
        ]),
      ),
    ),
    { initialValue: {} as Record<string, string> },
  );

  private readonly langLoaded = toSignal(this.translate.onLangChange, { initialValue: null });

  /** Dopasowanie po tytule LUB po treści tematu, bez uwzględniania wielkości liter i ogonków. */
  protected readonly filteredTopics = computed(() => {
    this.langLoaded();
    const q = normalizeText(this.query());
    if (!q) return this.topics;

    const contents = this.topicContents();
    return this.topics.filter((t) => {
      if (normalizeText(this.translate.instant(t.labelKey)).includes(q)) return true;
      const content = contents[t.key];
      return !!content && normalizeText(content).includes(q);
    });
  });
}
