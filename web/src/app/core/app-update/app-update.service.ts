import { Injectable, computed, inject, signal } from '@angular/core';
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

/** Wynik ostatniego sprawdzenia, pokazywany w Ustawieniach → „O aplikacji” (makieta Figma 420:1675, 420:1794, 420:1918). */
export type UpdateCheckStatus = 'idle' | 'checking' | 'upToDate' | 'available' | 'error';

interface KnownRelease {
  readonly latest: string;
  readonly apkUrl: string;
}

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
 * wersja ma ten sam podpis i wyższy numer. Brak internetu albo błąd GitHuba = brak paska, bez komunikatu — chyba że
 * użytkownik sam kazał sprawdzić w „O aplikacji”: wtedy wynik, także błąd, jest zawsze widać.
 */
@Injectable({ providedIn: 'root' })
export class AppUpdateService {
  private readonly http = inject(HttpClient);

  private readonly availableState = signal<AvailableUpdate | null>(null);
  /** Pasek pod nagłówkiem: jest, gdy jest nowsza wersja i użytkownik nie zamknął jej krzyżykiem. */
  readonly available = this.availableState.asReadonly();

  private readonly statusState = signal<UpdateCheckStatus>('idle');
  readonly status = this.statusState.asReadonly();

  private readonly installedState = signal<string | null>(null);
  readonly installed = this.installedState.asReadonly();

  /** Najnowsze znane wydanie — też wtedy, gdy pasek jest zamknięty, bo „O aplikacji” ma pokazać i tak. */
  private readonly releaseState = signal<KnownRelease | null>(null);
  readonly latest = computed(() => this.releaseState()?.latest ?? null);

  private readonly checkedAtState = signal<number | null>(readNumber(CheckedAtKey) || null);
  /** Kiedy ostatnio udało się zapytać GitHuba (ms), albo `null`, jeśli jeszcze nigdy. */
  readonly checkedAt = this.checkedAtState.asReadonly();

  /** Sprawdzanie wersji ma sens tylko w aplikacji na Androida — tylko tam „O aplikacji” jest w Ustawieniach. */
  get isSupported(): boolean {
    return this.isAndroidApp();
  }

  /**
   * Start aplikacji i każdy powrót do niej. Poza aplikacją na Androida nic nie robi.
   *
   * ⚠️ Zimny start omija godzinną blokadę: to jedyny moment, w którym użytkownik oczekuje informacji o nowej
   * wersji, a blokada (czas zapisany na telefonie) kazała mu czekać do godziny po ostatnim otwarciu. Blokada zostaje
   * przy powrotach z tła — te bywają co kilka minut i nie ma po co pytać GitHuba za każdym.
   */
  start(): void {
    if (!this.isAndroidApp()) return;
    void this.check(true);
    this.listenForResume(() => void this.check());
  }

  async check(force = false): Promise<void> {
    await this.run(force, false);
  }

  /** „Sprawdź aktualizacje” w Ustawieniach: bez blokady, a znaleziona wersja wraca na pasek nawet po zamknięciu go krzyżykiem. */
  async checkNow(): Promise<void> {
    await this.run(true, true);
  }

  /** Wersja zainstalowanej aplikacji do ekranu „O aplikacji”, zanim padnie pierwsze sprawdzenie. */
  async loadInstalled(): Promise<void> {
    try {
      this.installedState.set(await this.installedVersion());
    } catch {
      // Bez wersji ekran pokaże „—”; sprawdzenie i tak ją odczyta ponownie.
    }
  }

  private async run(force: boolean, showDismissed: boolean): Promise<void> {
    if (!force && Date.now() - readNumber(CheckedAtKey) < CheckIntervalMs) return;

    this.statusState.set('checking');
    try {
      const [current, release] = await Promise.all([
        this.installedVersion(),
        firstValueFrom(this.http.get<GithubRelease>(LatestReleaseUrl)),
      ]);
      // Dopiero po odpowiedzi: nieudane sprawdzenie nie może blokować następnego na godzinę.
      const now = Date.now();
      write(CheckedAtKey, String(now));
      this.checkedAtState.set(now);
      this.installedState.set(current);

      if (!release.tag_name.startsWith(TagPrefix)) {
        // Najnowsze wydanie repozytorium nie jest wydaniem Androida — nie wiadomo, czy jest nowsza wersja.
        this.statusState.set('error');
        return;
      }

      const latest = release.tag_name.slice(TagPrefix.length);
      const apkUrl = release.assets?.find((a) => a.name.endsWith('.apk'))?.browser_download_url ?? FallbackApkUrl;
      this.releaseState.set({ latest, apkUrl });

      if (!isNewerVersion(latest, current)) {
        this.availableState.set(null);
        this.statusState.set('upToDate');
        return;
      }

      this.statusState.set('available');
      if (showDismissed) clear(DismissedKey);
      if (read(DismissedKey) === latest) return;
      this.availableState.set({ current, latest, apkUrl });
    } catch {
      // Bez sieci albo przy limicie GitHuba nie ma paska — spróbujemy przy następnym powrocie po godzinie.
      // „O aplikacji” pokazuje wtedy błąd, bo użytkownik sam o to pytał.
      this.statusState.set('error');
    }
  }

  /** Pobranie w przeglądarce telefonu; Android po pobraniu pyta, czy zaktualizować aplikację. */
  update(): void {
    const release = this.releaseState();
    if (release && this.statusState() === 'available') void Browser.open({ url: release.apkUrl });
  }

  /** Chowa pasek do następnej wersji. */
  dismiss(): void {
    const update = this.availableState();
    if (update) write(DismissedKey, update.latest);
    this.availableState.set(null);
  }

  protected listenForResume(onResume: () => void): void {
    void CapacitorApp.addListener('resume', onResume);
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

function clear(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // Bez localStorage nie ma czego czyścić.
  }
}

function write(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Bez localStorage pasek wróci po ponownym uruchomieniu — to wszystko.
  }
}
