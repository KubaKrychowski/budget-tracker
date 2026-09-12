import { ErrorHandler, Injectable, NgZone, inject } from '@angular/core';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ErrorMessages } from './error-messages';
import { isReportable } from './api-error';

/**
 * Globalny handler błędów — ostatnia siatka pod tym, czego nie złapał żaden ekran.
 *
 * Powstał po błędzie, którego objaw był kompletnie mylący: odpowiedź 400 na liście transakcji
 * zostawiała kręcący się w nieskończoność spinner nad pustą tabelą. Wyjątek leciał do domyślnego
 * handlera Angulara, czyli do konsoli — a użytkownik przed ekranem nie miał ŻADNEJ przesłanki,
 * że cokolwiek się zepsuło. Cichy błąd jest gorszy od głośnego: przy głośnym wiadomo, że trzeba
 * odświeżyć; przy cichym czeka się w nieskończoność.
 *
 * ⚠️ To NIE jest miejsce na obsługę błędów, które ekran umie obsłużyć sam. Ekran, który wie,
 * co poszło nie tak, powinien pokazać stan błędu w swoim miejscu (patrz `errorOf` w liście
 * transakcji). Tutaj lądują wyłącznie te, o których nikt nie pomyślał — i dlatego komunikat
 * jest ogólny.
 */
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly messages = inject(ErrorMessages);
  private readonly message = inject(NzMessageService);
  private readonly zone = inject(NgZone);

  /**
   * Ostatnio pokazany komunikat i moment pokazania.
   *
   * Bez tego jeden błąd renderowania potrafi wyprodukować kilka identycznych powiadomień:
   * przy tamtym 400 leciały CZTERY, bo Angular ponawia render. Wieża tych samych toastów
   * nie niesie więcej informacji niż jeden, a zasłania ekran.
   */
  private last = { text: '', at: 0 };

  private static readonly DedupeWindowMs = 3000;

  handleError(error: unknown): void {
    // Konsola ZAWSZE, i to pierwsza: toast jest dla użytkownika, stos wywołań dla nas.
    // Gdyby cokolwiek niżej rzuciło, ślad po błędzie i tak zostanie.
    console.error(error);

    if (!isReportable(error)) return;

    const text = this.messages.of(unwrap(error));
    const now = Date.now();
    if (text === this.last.text && now - this.last.at < GlobalErrorHandler.DedupeWindowMs) return;
    this.last = { text, at: now };

    // Błąd potrafi przylecieć spoza strefy Angulara (np. z odrzuconej obietnicy). Bez `run`
    // powiadomienie zostałoby utworzone, ale nie doczekałoby się wykrywania zmian.
    this.zone.run(() => this.message.error(text));
  }
}

/**
 * Wyłuskuje pierwotny błąd HTTP z opakowania.
 *
 * Angular w stanie błędu zasobu rzuca własny `Error` z oryginałem w polu `cause` — bez tego
 * kroku użytkownik dostałby techniczne „Resource is currently in an error state" zamiast
 * zdania, które API o tym błędzie powiedziało.
 */
function unwrap(error: unknown): unknown {
  return error instanceof Error && error.cause !== undefined ? error.cause : error;
}
