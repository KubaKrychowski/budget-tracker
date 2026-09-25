import { beforeEach } from 'vitest';

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
