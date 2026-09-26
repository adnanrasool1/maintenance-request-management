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
  {
    path: 'requests/new',
    title: 'New request',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/requests/request-create/request-create.component').then(
        (m) => m.RequestCreateComponent,
      ),
  },
  {
    path: 'requests/:id',
    title: 'Request',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/requests/request-detail/request-detail.component').then(
        (m) => m.RequestDetailComponent,
      ),
  },
  { path: '', pathMatch: 'full', redirectTo: 'requests' },
  { path: '**', redirectTo: 'requests' },
];
