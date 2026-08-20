import { Routes } from '@angular/router';

import { DashboardComponent } from './features/dashboard/dashboard';
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
        component: DashboardComponent
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
