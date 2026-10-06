import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Capacitor } from '@capacitor/core';
import { App as CapacitorApp } from '@capacitor/app';
import { Browser } from '@capacitor/browser';

/** Najnowsze wydanie aplikacji na Androida — to samo, do którego linkuje landing (workflow `release-android.yml`). */
const LatestReleaseUrl = 'https://api.github.com/repos/KubaKrychowski/budget-tracker/releases/latest';
const FallbackApkUrl = 'https://github.com/KubaKrychowski/budget-tracker/releases/latest/download/wydatki.apk';
const TagPrefix = 'android-v';

/** Najwyżej jedno pytanie GitHuba na godzinę — API bez logowania ma limit 60 zapytań na godzinę na adres IP. */
const CheckIntervalMs = 60 * 60 * 1000;

/**
 * ⚠️ Prefiks `bt-`, nie `bt.`: wylogowanie czyści klucze `bt.` (App.clearLocalAppState), a zamknięcie paska dotyczy
 * telefonu, nie konta — po zmianie konta pasek tej samej wersji nie ma wracać.
 */
const DismissedKey = 'bt-app-update.dismissed';
const CheckedAtKey = 'bt-app-update.checked-at';

export interface AvailableUpdate {
  readonly current: string;
  readonly latest: string;
  readonly apkUrl: string;
}

interface GithubRelease {
  readonly tag_name: string;
  readonly assets?: readonly { readonly name: string; readonly browser_download_url: string }[];
}

/**
 * `true`, gdy `latest` jest nowsza od `current`. Porównanie po liczbach w kolejnych członach („2.10” > „2.9”),
 * brakujący człon liczy się jak 0 („2.1” == „2.1.0”). Człon, który nie jest liczbą, też liczy się jak 0.
 */
export function isNewerVersion(latest: string, current: string): boolean {
  const parse = (v: string) => v.split('.').map((part) => Number.parseInt(part, 10) || 0);
  const a = parse(latest);
  const b = parse(current);
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    const diff = (a[i] ?? 0) - (b[i] ?? 0);
    if (diff !== 0) return diff > 0;
  }
  return false;
}

/**
 * Pasek „Jest nowa wersja aplikacji” (makieta Figma „Mobile — pasek aktualizacji aplikacji”).
 *
 * @remarks
 * Tylko w aplikacji na Androida: w przeglądarce front aktualizuje się sam przy wdrożeniu, a na iPhonie aplikacji nie ma.
 * Instalacja to zwykłe pobranie APK — Android sam pyta „Zaktualizować?”, a dane i zalogowanie zostają, bo nowa
 * wersja ma ten sam podpis i wyższy numer. Brak internetu albo błąd GitHuba = brak paska, bez komunikatu.
 */
@Injectable({ providedIn: 'root' })
export class AppUpdateService {
  private readonly http = inject(HttpClient);

  private readonly availableState = signal<AvailableUpdate | null>(null);
  readonly available = this.availableState.asReadonly();

  /** Start aplikacji i każdy powrót do niej. Poza aplikacją na Androida nic nie robi. */
  start(): void {
    if (!this.isAndroidApp()) return;
    void this.check();
    void CapacitorApp.addListener('resume', () => void this.check());
  }

  async check(force = false): Promise<void> {
    if (!force && Date.now() - readNumber(CheckedAtKey) < CheckIntervalMs) return;

    try {
      const [current, release] = await Promise.all([
        this.installedVersion(),
        firstValueFrom(this.http.get<GithubRelease>(LatestReleaseUrl)),
      ]);
      // Dopiero po odpowiedzi: nieudane sprawdzenie nie może blokować następnego na godzinę.
      write(CheckedAtKey, String(Date.now()));
      if (!release.tag_name.startsWith(TagPrefix)) return;

      const latest = release.tag_name.slice(TagPrefix.length);
      if (!isNewerVersion(latest, current) || read(DismissedKey) === latest) return;

      const apk = release.assets?.find((a) => a.name.endsWith('.apk'));
      this.availableState.set({ current, latest, apkUrl: apk?.browser_download_url ?? FallbackApkUrl });
    } catch {
      // Bez sieci albo przy limicie GitHuba po prostu nie ma paska — spróbujemy przy następnym powrocie po godzinie.
    }
  }

  /** Pobranie w przeglądarce telefonu; Android po pobraniu pyta, czy zaktualizować aplikację. */
  update(): void {
    const update = this.availableState();
    if (update) void Browser.open({ url: update.apkUrl });
  }

  /** Chowa pasek do następnej wersji. */
  dismiss(): void {
    const update = this.availableState();
    if (update) write(DismissedKey, update.latest);
    this.availableState.set(null);
  }

  /** Wersja zainstalowanej aplikacji (versionName z taga wydania, np. „2.1”). */
  protected async installedVersion(): Promise<string> {
    return (await CapacitorApp.getInfo()).version;
  }

  protected isAndroidApp(): boolean {
    return Capacitor.isNativePlatform() && Capacitor.getPlatform() === 'android';
  }
}

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function readNumber(key: string): number {
  return Number(read(key) ?? 0) || 0;
}

function write(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Bez localStorage pasek wróci po ponownym uruchomieniu — to wszystko.
  }
}
