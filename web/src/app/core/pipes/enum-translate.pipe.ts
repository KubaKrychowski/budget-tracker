import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

/**
 * Tłumaczy wartość enuma przychodzącą z API na tekst dla użytkownika.
 *
 * Backend wysyła enumy jako angielskie identyfikatory (`PendingReview`, `LunchCard`),
 * bo to nazwy w kodzie, nie treść. Zamiana na język użytkownika należy do frontu:
 *
 * ```html
 * {{ transaction.status | enumTranslate: 'transactionStatus' }}
 * ```
 *
 * `pure: false` — tak samo jak `TranslatePipe` z ngx-translate. Pipe czysty policzyłby
 * się raz i został przy wartości z chwili pierwszego renderu, więc przełączenie języka
 * nie odświeżyłoby tekstu.
 */
@Pipe({ name: 'enumTranslate', pure: false })
export class EnumTranslatePipe implements PipeTransform {
  private readonly translate = inject(TranslateService);

  transform(value: string | null | undefined, enumName: string): string {
    if (!value) return '';

    const key = `enums.${enumName}.${value}`;
    const translated = this.translate.instant(key);

    // ngx-translate przy braku wpisu oddaje sam klucz. Pokazanie użytkownikowi
    // „enums.transactionStatus.Foo" jest gorsze niż surowa wartość z API,
    // więc w takim wypadku wracamy do niej.
    return translated === key ? value : translated;
  }
}
