import { RouterOutletComponent } from '@abp/ng.core';
import { Routes } from '@angular/router';

export const SITE_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    component: RouterOutletComponent,
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./components/site.component').then(c => c.SiteComponent),
      },
    ],
  },
];
