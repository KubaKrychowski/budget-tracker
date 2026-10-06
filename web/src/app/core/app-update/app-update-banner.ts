import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AppUpdateService } from './app-update.service';

/** Pasek „Jest nowa wersja aplikacji” pod nagłówkiem (makieta Figma „Mobile — pasek aktualizacji aplikacji”). */
@Component({
  selector: 'app-update-banner',
  imports: [TranslatePipe, NzButtonModule, NzIconModule],
  template: `
    @if (updates.available(); as update) {
      <div class="upd" role="status">
        <nz-icon class="upd__icon" nzType="download" />
        <span class="upd__text">
          <strong>{{ 'appUpdate.title' | translate }}</strong>
          <span class="upd__versions">{{ 'appUpdate.versions' | translate: { latest: update.latest, current: update.current } }}</span>
        </span>
        <button nz-button nzType="primary" nzSize="small" (click)="updates.update()">{{ 'appUpdate.update' | translate }}</button>
        <button
          type="button"
          class="upd__close"
          [attr.aria-label]="'appUpdate.dismiss' | translate"
          [title]="'appUpdate.dismiss' | translate"
          (click)="updates.dismiss()"
        >
          <nz-icon nzType="close" />
        </button>
      </div>
    }
  `,
  styles: `
    .upd {
      display: flex;
      align-items: center;
      gap: 10px;
      margin: 0 16px 12px;
      padding: 10px 8px 10px 12px;
      background: var(--ds-info-50);
      border: 1px solid var(--ds-info-500);
      border-radius: 8px;
    }
    .upd__icon { flex: none; font-size: 20px; color: var(--ds-info-500); }
    .upd__text { display: flex; flex: 1 1 auto; flex-direction: column; min-width: 0; }
    .upd__text strong { font-size: 14px; font-weight: 500; }
    .upd__versions { font-size: 12px; color: var(--ds-text-description); }
    .upd__close {
      display: inline-flex;
      padding: 4px;
      font-size: 16px;
      color: var(--ds-neutral-500);
      background: none;
      border: none;
      cursor: pointer;
    }
  `,
})
export class AppUpdateBanner {
  protected readonly updates = inject(AppUpdateService);
}
