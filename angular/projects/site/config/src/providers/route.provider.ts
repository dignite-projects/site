import { eLayoutType, RoutesService } from '@abp/ng.core';
import { provideFlexFields } from '@dignite/ng.flex-fields';
import { SEO_FIELD_TYPE } from '@dignite/site';
import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { eSiteRouteNames } from '../enums/route-names';

export const SITE_ROUTE_PROVIDERS = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

export function configureRoutes() {
  const routesService = inject(RoutesService);
  routesService.add([
    {
      path: '/site',
      name: eSiteRouteNames.Site,
      iconClass: 'fas fa-sitemap',
      layout: eLayoutType.application,
      order: 3,
      requiredPolicy:
        'Admin.Pages || Admin.ContentTypes || Admin.Fields || Admin.Contents',
    },
    {
      path: '/site/contents',
      name: eSiteRouteNames.Contents,
      parentName: eSiteRouteNames.Site,
      iconClass: 'fas fa-file-lines',
      layout: eLayoutType.application,
      order: 1,
      requiredPolicy: 'Admin.Contents',
    },
    {
      path: '/site/pages',
      name: eSiteRouteNames.Pages,
      parentName: eSiteRouteNames.Site,
      iconClass: 'fas fa-file-code',
      layout: eLayoutType.application,
      order: 2,
      requiredPolicy: 'Admin.Pages',
    },
    {
      path: '/site/fields',
      name: eSiteRouteNames.Fields,
      parentName: eSiteRouteNames.Site,
      iconClass: 'fas fa-list-check',
      layout: eLayoutType.application,
      order: 3,
      requiredPolicy: 'Admin.Fields',
    },
  ]);

  // Content types have no menu entry: they belong to a page, and are reached from the Pages list.
}

const SITE_PROVIDERS: EnvironmentProviders[] = [
  ...SITE_ROUTE_PROVIDERS,
  // The six built-ins plus Site's own `Seo` type. `FieldTypeResolver` is root-provided and reads the
  // registry once when first injected, so this has to happen at application-config level - registering
  // from inside the lazy-loaded Site routes would come too late for a resolver already constructed.
  provideFlexFields(SEO_FIELD_TYPE),
];

export function provideSite() {
  return makeEnvironmentProviders(SITE_PROVIDERS);
}
