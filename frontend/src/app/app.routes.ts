import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'requests',
    title: 'Requests',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/requests/request-list/request-list.component').then(
        (m) => m.RequestListComponent,
      ),
  },
  { path: '', pathMatch: 'full', redirectTo: 'requests' },
  { path: '**', redirectTo: 'requests' },
];
