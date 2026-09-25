import { beforeEach } from 'vitest';
import { setRuntimeConfigForTests } from './app/core/runtime-config';

/**
 * Adresy backendów, które w przeglądarce przychodzą z `config.json`, a w testach nie mają skąd.
 *
 * ⚠️ Musi być USTAWIONE, zanim którykolwiek plik testu zaimportuje `app.config.ts` — ten moduł czyta
 * adres serwera tożsamości już w swoim ciele, a bez konfiguracji celowo rzuca wyjątkiem (patrz
 * `runtime-config.ts`). `setupFiles` w Vitest wykonują się przed plikami testów, więc to jest to miejsce.
 */
setRuntimeConfigForTests({ identityAuthority: 'https://localhost:7226', apiBaseUrl: '' });

/**
 * Czysty `localStorage` przed KAŻDYM testem.
 *
 * ⚠️ Powód nie jest kosmetyczny. Kilka serwisów pamięta stan przeglądarki między sesjami
 * (`ActiveBudget`, `budget-range-memory`, historia wyszukiwań, ostatnie ekrany), a jdom dzieli
 * storage między plikami testów w tym samym procesie. Skutkiem był test, który przechodził
 * uruchomiony sam i padał w pełnym przebiegu: `savings.spec.ts` sprawdzał, że bez wskazanego
 * budżetu zapytanie NIE niesie `budgetId`, a dostawał budżet zapamiętany przez wcześniejszy plik.
 *
 * Kolejność plików w Vitest zależy od czasu ich trwania, więc taka zależność potrafi się ujawnić
 * dopiero po dołożeniu niezwiązanego testu — i wygląda wtedy jak jego wina.
 */
beforeEach(() => {
  try {
    localStorage.clear();
    sessionStorage.clear();
  } catch {
    // Środowisko bez storage — nie ma czego czyścić.
  }
});
