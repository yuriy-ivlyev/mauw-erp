import { Routes } from '@angular/router';
import { AdminLayoutComponent } from './shared/layout/admin-layout.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';

export const routes: Routes = [
  {
    path: '',
    component: AdminLayoutComponent,
    children: [
      {
        path: '',
        component: DashboardComponent,
        pathMatch: 'full',
        title: 'Dashboard | Mauw ERP'
      },
      {
        path: 'dashboard',
        component: DashboardComponent,
        title: 'Dashboard | Mauw ERP'
      }
    ]
  }
];
