import { Injectable, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { App as CapacitorApp } from '@capacitor/app';

/**
 * Wersja aplikacji do napisu pod nazwą (okładka logowania i nagłówek).
 *
 * @remarks
 * ⚠️ W aplikacji natywnej to `versionName` z taga wydania (np. „2.4”) — ten sam numer, który czyta „O aplikacji”.
 * Napis „v.2.1” wpisany na sztywno w tłumaczeniach (`app.version`) nie nadążał za wydaniami: telefon z 2.4 dalej
 * pokazywał 2.1. W przeglądarce numeru wydania nie ma (front wdraża się ciągle, bez wersji), więc tam zostaje
 * napis z tłumaczeń.
 */
@Injectable({ providedIn: 'root' })
export class AppVersion {
  private readonly nativeState = signal<string | null>(null);

  /** Numer zainstalowanej aplikacji (np. „2.4”), albo `null` w przeglądarce i zanim wtyczka odpowie. */
  readonly native = this.nativeState.asReadonly();

  constructor() {
    if (!this.isNativeApp()) return;
    void this.readInstalled().then((version) => this.nativeState.set(version), () => undefined);
  }

  /** Napis z numerem w formacie dotychczasowego `app.version`: „v.2.4”. */
  label(version: string): string {
    return `v.${version}`;
  }

  protected isNativeApp(): boolean {
    return Capacitor.isNativePlatform();
  }

  protected async readInstalled(): Promise<string> {
    return (await CapacitorApp.getInfo()).version;
  }
}
