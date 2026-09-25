import { Component } from '@angular/core';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';

/**
 * Landing — prezentacja PROJEKTU, nie landing produktu.
 *
 * <para>
 * Rozstrzygnięcie z issue #12: adresatem jest ktoś oceniający warsztat, nie ktoś szukający
 * narzędzia do budżetu. Powód jest twardy, nie estetyczny — aplikacja jest single-user,
 * bez rejestracji i bez hostingu, więc przycisk „Wypróbuj za darmo” nie ma dokąd prowadzić.
 * Strona, która obiecuje coś, czego nie ma czym spełnić, szkodzi bardziej niż jej brak.
 * </para>
 *
 * <para>
 * ⚠️ Cała treść jest tu na sztywno po polsku, wbrew konwencji aplikacji (teksty w
 * `public/i18n/*.json`). To świadome odstępstwo: landing jest jednojęzyczny z założenia
 * (wersja angielska jest jawnie poza zakresem #12), a przepuszczenie prozy marketingowej
 * przez klucze tłumaczeń zamieniłoby tekst możliwy do przeczytania i ocenienia w recenzji
 * na listę identyfikatorów. Gdy dojdzie druga wersja językowa, to się zmienia.
 * </para>
 *
 * <para>
 * Sekcje NIE są osobnymi komponentami. Landing to dokument liniowy bez stanu i bez powtórzeń —
 * siedem komponentów po jednym użyciu byłoby rusztowaniem na zapas, którego ten projekt unika
 * (CLAUDE.md §10). Jeśli któraś sekcja zacznie żyć własnym życiem, wtedy się ją wyciągnie.
 * </para>
 */
@Component({
  selector: 'app-root',
  imports: [NzAlertModule, NzButtonModule, NzDividerModule, NzIconModule, NzTagModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  /**
   * Jedyne prawdziwe CTA na tej stronie — patrz doc klasy.
   *
   * ⚠️ Musi wskazywać repozytorium **publiczne**. Do 2026-09-12 stał tu adres repo prywatnego,
   * czyli jedyny przycisk na stronie dawał każdemu odwiedzającemu 404 — a to psuje dokładnie
   * to, po co ta strona istnieje. Kod przeniósł się do `budget-tracker`, bo starego repo nie
   * dało się bezpiecznie upublicznić (historia i `refs/pull/*` przeżywają force-push).
   * Pilnuje tego `app.spec.ts`: sprawdza konkretny adres, nie samo „jest w domenie github.com".
   */
  protected readonly repoUrl = 'https://github.com/KubaKrychowski/budget-tracker';

  /**
   * Adres lokalnej instancji aplikacji.
   *
   * ⚠️ REWIZJA decyzji z issue #12 (2026-09-25, na prośbę właściciela). Landing celowo NIE miał
   * przycisku do aplikacji: bez hostingu i rejestracji prowadziłby donikąd, a strona obiecująca
   * coś, czego nie ma czym spełnić, szkodzi bardziej niż jej brak. Przycisk wraca, bo dziś ta
   * strona jest serwowana WYŁĄCZNIE lokalnie (`ng serve landing`), obok aplikacji na 4200 —
   * w tym jedynym użyciu link prowadzi dokładnie tam, gdzie zapowiada.
   *
   * ⚠️ REWIZJA (2026-09-25, wieczór): aplikacja stoi już na Static Web Apps, więc adres przestał być
   * lokalny. To był dokładnie ten moment, przed którym ostrzegała poprzednia wersja tego komentarza:
   * `localhost` u obcego czytelnika to jedyne CTA prowadzące donikąd.
   *
   * ⚠️ Adres jest wpisany na sztywno, bo landing nie ma konfiguracji wczytywanej w czasie działania
   * (aplikacja ma — patrz `core/runtime-config.ts`). Host Static Web Apps ma losowy człon nadany przy
   * tworzeniu zasobu, więc po odtworzeniu środowiska od zera ten adres trzeba tu podmienić.
   */
  protected readonly appUrl = 'https://salmon-cliff-0ee2c9f03.3.azurestaticapps.net';

  /**
   * DOMYŚLNY próg pewności, poniżej którego transakcja idzie do przeglądu zamiast dostać kategorię.
   *
   * ⚠️ To kopia wartości z backendu (`MlCategorizer.ConfidenceThreshold`, domyślnie `0.7m`,
   * nadpisywalna w `appsettings`), a nie jej źródło — i NIC tego nie synchronizuje. Landing jest
   * osobnym projektem, więc nie ma tu testu, który złapałby zmianę progu po stronie API.
   * Dlatego tekst na stronie mówi „domyślnie”, zamiast podawać tę liczbę jako prawo: sam próg
   * jest w CLAUDE.md §9 opisany jako ZAŁOŻENIE do strojenia na realnych danych.
   *
   * Stoi na stronie mimo to, bo „model czasem się myli” bez liczby jest ogólnikiem,
   * a z liczbą jest sprawdzalnym opisem mechanizmu.
   */
  protected readonly reviewThreshold = 0.7;
}
