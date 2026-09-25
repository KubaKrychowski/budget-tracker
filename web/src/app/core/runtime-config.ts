/**
 * Adresy backendów wczytywane W CZASIE DZIAŁANIA, z `config.json` obok zbudowanej aplikacji.
 *
 * Powód: ta sama paczka ma działać lokalnie i na Static Web Apps. Adresy wpisane na sztywno w kodzie
 * znaczyłyby osobny build na środowisko, a przy Static Web Apps — build w akcji GitHuba, który musiałby
 * znać adresy zanim Terraform je utworzy (host SWA ma losowy człon i nie da się go przewidzieć).
 */
export interface RuntimeConfig {
  /** Adres BudgetTracker.Identity — wystawca tokenów i strony logowania, 2FA, usuwania konta (Razor). */
  identityAuthority: string;

  /**
   * Adres API budżetu. Puste = ten sam origin co aplikacja (tak działa dev z proxy `ng serve`).
   * Na Static Web Apps musi być pełnym adresem, bo plan Free nie ma „linked backend" i front woła
   * API między originami, przez CORS.
   */
  apiBaseUrl: string;
}

let current: RuntimeConfig | null = null;

/**
 * ⚠️ Rzuca, gdy konfiguracji nie da się wczytać, zamiast po cichu wrócić do wartości deweloperskich.
 * Cicha wartość domyślna znaczyłaby aplikację wdrożoną na Azure, która puka do `localhost` i zachowuje
 * się niezrozumiale: logowanie wisi, żądania nie wychodzą, a w konsoli nie ma nic sensownego.
 */
export async function loadRuntimeConfig(): Promise<void> {
  const response = await fetch('config.json', { cache: 'no-cache' });

  if (!response.ok) {
    throw new Error(`Nie udało się wczytać config.json (HTTP ${response.status}).`);
  }

  const loaded = (await response.json()) as Partial<RuntimeConfig>;

  if (!loaded.identityAuthority) {
    throw new Error('config.json nie zawiera identityAuthority.');
  }

  current = {
    identityAuthority: loaded.identityAuthority,
    apiBaseUrl: loaded.apiBaseUrl ?? '',
  };
}

/**
 * ⚠️ Wartości deweloperskie są tu wyłącznie dla testów i narzędzi, które ładują moduły aplikacji bez
 * przeglądarki. W przeglądarce nie da się na nich wylądować: `main.ts` czeka na `loadRuntimeConfig()`,
 * a ono RZUCA, gdy `config.json` nie wczyta się poprawnie — więc aplikacja z niekompletną konfiguracją
 * w ogóle nie wystartuje, zamiast pukać do `localhost` z wdrożonego adresu.
 *
 * Fałszywy start nie wchodzi w grę także dlatego, że builder testów Angulara ładuje każdy plik testu
 * z własnym rejestrem modułów — wartość ustawiona w `setupFiles` nie jest widoczna w module wczytanym
 * przez plik testu, więc rzucanie stąd wywracało testy niezwiązane z konfiguracją.
 */
const developmentDefaults: RuntimeConfig = {
  identityAuthority: 'https://localhost:7226',
  apiBaseUrl: '',
};

export function runtimeConfig(): RuntimeConfig {
  return current ?? developmentDefaults;
}

/** Wyłącznie dla testów: ustawia konfigurację bez sięgania po sieć. */
export function setRuntimeConfigForTests(config: RuntimeConfig): void {
  current = config;
}
