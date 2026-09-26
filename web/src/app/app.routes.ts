import { Routes } from '@angular/router';
import { autoLoginPartialRoutesGuard } from 'angular-auth-oidc-client';

/**
 * Cała aplikacja pod jednym strażnikiem logowania — niezalogowany użytkownik nigdy nie widzi
 * powłoki (`App`), tylko przekierowanie do ekranu logowania BudgetTracker.Identity. `auth-callback`
 * jest CELOWO wewnątrz tej samej gałęzi: to na ten adres wraca przeglądarka po zalogowaniu z kodem
 * autoryzacyjnym w query stringu, a strażnik sam go rozpoznaje i wymienia na token, zanim
 * jakikolwiek ekran się wyrenderuje.
 */
export const routes: Routes = [
  {
    path: '',
    canActivate: [autoLoginPartialRoutesGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'auth-callback', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.Dashboard),
        title: 'Dashboard — Budżet tracker',
      },
      {
        path: 'import',
        loadComponent: () =>
          import('./features/import/import').then((m) => m.Import),
        title: 'Import wyciągu — Wydatki.com',
      },
      {
        path: 'transactions',
        loadComponent: () =>
          import('./features/transactions/transactions').then((m) => m.Transactions),
        title: 'Lista transakcji — Wydatki.com',
      },
      {
        path: 'savings',
        loadComponent: () =>
          import('./features/savings/savings').then((m) => m.Savings),
        title: 'Cele oszczędzania — Wydatki.com',
      },
      {
        path: 'savings/reservations',
        loadComponent: () =>
          import('./features/reservations/reservations').then((m) => m.Reservations),
        title: 'Wszystkie rezerwacje — Wydatki.com',
      },
      {
        path: 'limits',
        loadComponent: () =>
          import('./features/limits/limits').then((m) => m.Limits),
        title: 'Limity wydatków — Wydatki.com',
      },
      {
        path: 'standing-orders',
        loadComponent: () =>
          import('./features/standing-orders/standing-orders').then((m) => m.StandingOrders),
        title: 'Zlecenia stałe — Wydatki.com',
      },
      {
        path: 'episodic-orders',
        loadComponent: () =>
          import('./features/episodic-orders/episodic-orders').then((m) => m.EpisodicOrders),
        title: 'Zlecenia epizodyczne — Wydatki.com',
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/settings/settings').then((m) => m.Settings),
        title: 'Ustawienia — Wydatki.com',
      },
      {
        path: 'create-budget',
        loadComponent: () =>
          import('./features/create-budget/create-budget').then((m) => m.CreateBudget),
        title: 'Tworzenie budzetu',
      },
      {
        path: 'functions',
        loadComponent: () =>
          import('./features/functions/functions').then((m) => m.Functions),
        title: 'Wszystkie funkcje — Wydatki.com',
      },
      {
        path: 'handbook',
        loadComponent: () =>
          import('./features/handbook/handbook').then((m) => m.Handbook),
        title: 'Podręcznik — Wydatki.com',
      },
    ],
  },
];
