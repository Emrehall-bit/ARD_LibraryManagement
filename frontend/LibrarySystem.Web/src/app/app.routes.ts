import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { AppLayoutComponent } from './layout/app-layout/app-layout';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((component) => component.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register').then((component) => component.RegisterComponent)
  },
  {
    path: '',
    component: AppLayoutComponent,
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        canActivate: [authGuard],
        loadComponent: () => import('./features/dashboard/dashboard').then((component) => component.DashboardComponent)
      },
      {
        path: 'books',
        loadComponent: () => import('./features/books/books-page/books-page').then((component) => component.BooksPageComponent)
      },
      {
        path: 'my-books',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/borrowing/my-books-page/my-books-page').then((component) => component.MyBooksPageComponent)
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
