import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
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
    title: 'Import wyciągu — Budżet tracker',
  },
  {
    path: 'transactions',
    loadComponent: () =>
      import('./features/transactions/transactions').then((m) => m.Transactions),
    title: 'Lista transakcji — Budżet tracker',
  },
  {
    path: 'savings',
    loadComponent: () =>
      import('./features/savings/savings').then((m) => m.Savings),
    title: 'Cele oszczędzania — Budżet tracker',
  },
  {
    path: 'savings/reservations',
    loadComponent: () =>
      import('./features/reservations/reservations').then((m) => m.Reservations),
    title: 'Wszystkie rezerwacje — Budżet tracker',
  },
  {
    path: 'limits',
    loadComponent: () =>
      import('./features/limits/limits').then((m) => m.Limits),
    title: 'Limity wydatków — Budżet tracker',
  },
  {
    path: 'standing-orders',
    loadComponent: () =>
      import('./features/standing-orders/standing-orders').then((m) => m.StandingOrders),
    title: 'Zlecenia stałe — Budżet tracker',
  },
  {
    path: 'episodic-orders',
    loadComponent: () =>
      import('./features/episodic-orders/episodic-orders').then((m) => m.EpisodicOrders),
    title: 'Zlecenia epizodyczne — Budżet tracker',
  },
  {
    path: 'settings',
    loadComponent: () =>
      import('./features/settings/settings').then((m) => m.Settings),
    title: 'Ustawienia — Budżet tracker',
  },
  {
    path: 'create-budget',
    loadComponent: () =>
      import('./features/create-budget/create-budget').then((m) => m.CreateBudget),
    title: 'Tworzenie budzetu',
  },
];
