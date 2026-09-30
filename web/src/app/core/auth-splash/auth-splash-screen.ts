import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { switchMap } from 'rxjs';
import { AuthSplashStatus } from './auth-splash';

/**
 * Okładka logowania z makiety Figma (Dashboard → „Splash — sprawdzanie logowania” / „wczytywanie danych”).
 *
 * @remarks
 * Teksty czekają na plik tłumaczeń (`translate.get`), zamiast pokazać na chwilę klucz. Ten sam układ i te same
 * wymiary stoją statycznie w `index.html` (`#boot-splash`) na czas ładowania paczki — muszą zostać identyczne,
 * żeby przejście z jednej okładki do drugiej nie było widać.
 */
@Component({
  selector: 'app-auth-splash',
  templateUrl: './auth-splash-screen.html',
  styleUrl: './auth-splash-screen.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthSplashScreen {
  readonly status = input.required<AuthSplashStatus>();

  private readonly translate = inject(TranslateService);

  private readonly texts = toSignal(
    toObservable(this.status).pipe(
      switchMap((status) => this.translate.get(['app.title', 'app.version', `auth.splash.${status}`])),
    ),
    { initialValue: null },
  );

  protected readonly title = computed(() => this.texts()?.['app.title'] ?? '');
  protected readonly version = computed(() => this.texts()?.['app.version'] ?? '');
  protected readonly message = computed(() => this.texts()?.[`auth.splash.${this.status()}`] ?? '');
}
