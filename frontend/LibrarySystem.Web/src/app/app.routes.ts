import { Routes } from '@angular/router';

import { AppLayoutComponent } from './layout/app-layout/app-layout';

export const routes: Routes = [
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
        children: []
      },
      {
        path: 'books',
        children: []
      },
      {
        path: 'my-books',
        children: []
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
