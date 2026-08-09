import { authGuard, permissionGuard, RouterOutletComponent } from '@abp/ng.core';
import { Routes } from '@angular/router';
import { eSitePolicyNames } from './enums/policy-names';

/**
 * No `admin` path segment (unlike Dignite.Cms's `/cms/admin/...`): the public tier has no Angular UI,
 * so every route here is an admin route and the extra segment would carry no information.
 *
 * The parent deliberately has no `pathMatch: 'full'`. It did while this library was a stub, which
 * would have stopped any child route from ever matching.
 */
export const SITE_ROUTES: Routes = [
  {
    path: '',
    component: RouterOutletComponent,
    canActivate: [authGuard, permissionGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'contents',
      },
      {
        path: 'contents',
        loadComponent: () =>
          import('./components/contents/contents.component').then(c => c.ContentsComponent),
        data: { requiredPolicy: eSitePolicyNames.Contents },
      },
      {
        // Editing a content is a page, not a dialog: the left half is a form as long as the content
        // type defines, which a dialog could only scroll.
        path: 'contents/create',
        loadComponent: () =>
          import('./components/contents/content-editor.component').then(c => c.ContentEditorComponent),
        data: { requiredPolicy: eSitePolicyNames.ContentsCreate },
      },
      {
        path: 'contents/:id/edit',
        loadComponent: () =>
          import('./components/contents/content-editor.component').then(c => c.ContentEditorComponent),
        data: { requiredPolicy: eSitePolicyNames.ContentsUpdate },
      },
      {
        path: 'pages',
        loadComponent: () => import('./components/pages/pages.component').then(c => c.PagesComponent),
        data: { requiredPolicy: eSitePolicyNames.Pages },
      },
      {
        path: 'pages/:pageId/content-types',
        loadComponent: () =>
          import('./components/content-types/content-types.component').then(
            c => c.ContentTypesComponent,
          ),
        data: { requiredPolicy: eSitePolicyNames.ContentTypes },
      },
      {
        path: 'fields',
        loadComponent: () =>
          import('./components/fields/fields.component').then(c => c.FieldsComponent),
        data: { requiredPolicy: eSitePolicyNames.Fields },
      },
    ],
  },
];
