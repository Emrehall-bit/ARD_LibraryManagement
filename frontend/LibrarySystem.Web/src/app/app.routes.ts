import { Routes } from '@angular/router';

import { adminGuard } from './core/guards/admin.guard';
import { authGuard } from './core/guards/auth.guard';

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
    loadComponent: () => import('./layout/app-layout/app-layout').then((component) => component.AppLayoutComponent),
    children: [
      {
        path: '',
        redirectTo: 'books',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        canActivate: [authGuard],
        loadComponent: () => import('./features/dashboard/dashboard').then((component) => component.DashboardComponent)
      },
      {
        path: 'books/:id',
        loadComponent: () =>
          import('./features/books/book-detail-page/book-detail-page').then((component) => component.BookDetailPageComponent)
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
      },
      {
        path: 'borrow-history',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/borrowing/borrow-history-page/borrow-history-page').then(
            (component) => component.BorrowHistoryPageComponent
          )
      },
      {
        path: 'admin/overdue-borrows',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/borrowing/admin-overdue-borrows-page/admin-overdue-borrows-page').then(
            (component) => component.AdminOverdueBorrowsPageComponent
          )
      },
      {
        path: 'admin/users',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/auth/admin-users/admin-users-page/admin-users-page').then(
            (component) => component.AdminUsersPageComponent
          )
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
