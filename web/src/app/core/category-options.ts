import { CategoryOption } from './api/models/category-option';

/**
 * Opcje wyboru kategorii bez tych ukrytych przez konto (zgłoszenie #30) — ale Z kategorią już wybraną.
 *
 * ⚠️ Ukryta kategoria znika z list wyboru, lecz istniejące przypisania (transakcja, reguła, zlecenie) zostają w niej. Gdyby select
 * dostał listę bez niej, pole pokazałoby się puste, a zapis mógłby po cichu skasować kategorię. Dlatego aktualnie wybrana ukryta
 * kategoria zostaje w opcjach — dopóki użytkownik jej sam nie zmieni.
 */
export function pickerOptions(
  all: readonly CategoryOption[],
  current: string | null | undefined,
): readonly CategoryOption[] {
  return all.filter((c) => !c.hidden || c.id === current);
}
