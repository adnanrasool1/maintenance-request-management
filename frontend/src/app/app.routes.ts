import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  { path: '**', redirectTo: 'login' },
];
