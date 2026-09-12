import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { apiErrorText } from './api-error';

/**
 * Jedno miejsce, w którym błąd zamienia się w zdanie dla użytkownika.
 *
 * Powstało z pięciu identycznych kopii prywatnej metody `messageOf` — w liście transakcji,
 * imporcie, ustawieniach, danych treningowych i tworzeniu budżetu. CLAUDE.md §10 każe wyciągać
 * przy trzecim powtórzeniu; było piąte, a każda kopia sięgała po własny klucz tłumaczenia,
 * więc ten sam błąd sieci mówił co innego zależnie od ekranu.
 */
@Injectable({ providedIn: 'root' })
export class ErrorMessages {
  private readonly translate = inject(TranslateService);

  /**
   * Tekst z API, a gdy go nie ma — ogólny komunikat o nieudanym połączeniu.
   *
   * `fallbackKey` pozwala ekranowi dopowiedzieć swoje („nie udało się zapisać zmian"),
   * ale domyślnie wystarcza wspólny klucz: brak odpowiedzi z serwera znaczy to samo wszędzie.
   */
  of(error: unknown, fallbackKey = 'errors.network'): string {
    return apiErrorText(error) ?? this.translate.instant(fallbackKey);
  }
}
