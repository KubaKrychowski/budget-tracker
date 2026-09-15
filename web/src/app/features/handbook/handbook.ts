import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { MarkdownComponent } from 'ngx-markdown';
import { PageHeader } from '../../core/page-header/page-header';
import { HANDBOOK_TOPICS, handbookTopicByKey } from '../../core/handbook-topics';

/**
 * Podręcznik użytkownika (issue #19) — lista tematów po lewej, treść z `public/handbook/*.md`
 * po prawej. Wybrany temat siedzi w adresie (`?topic=`), tak jak wybór budżetu na innych
 * ekranach: da się zapisać w zakładkach i wraca po cofnięciu w przeglądarce.
 */
@Component({
  selector: 'app-handbook',
  imports: [RouterLink, TranslatePipe, NzBreadCrumbModule, MarkdownComponent, PageHeader],
  templateUrl: './handbook.html',
  styleUrl: './handbook.scss',
})
export class Handbook {
  private readonly route = inject(ActivatedRoute);

  protected readonly topics = HANDBOOK_TOPICS;

  private readonly queryTopic = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  protected readonly selected = computed(() => handbookTopicByKey(this.queryTopic().get('topic')));
}
