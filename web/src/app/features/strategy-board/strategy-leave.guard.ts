import { CanDeactivateFn } from '@angular/router';

/** Ekran, który potrafi powiedzieć, czy można go opuścić — tablica pyta użytkownika, gdy ma niezapisane zmiany. */
export interface LeaveAware {
  canLeave(): boolean | Promise<boolean>;
}

/**
 * Strażnik wyjścia z tablicy strategii: każde opuszczenie trasy (breadcrumb, menu, wstecz, inna strategia) przechodzi
 * przez okno „Wyjść bez zapisania?”, o ile są niezapisane zmiany.
 *
 * Zamknięcie karty i odświeżenie przeglądarki obsługuje osobno `beforeunload` w samym komponencie — strażnik trasy ich nie widzi.
 */
export const leaveStrategyGuard: CanDeactivateFn<LeaveAware> = (component) => component.canLeave();
