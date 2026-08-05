import { eLayoutType, RoutesService } from '@abp/ng.core';
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
      iconClass: 'fas fa-book',
      layout: eLayoutType.application,
      order: 3,
    },
  ]);
}

const SITE_PROVIDERS: EnvironmentProviders[] = [...SITE_ROUTE_PROVIDERS];

export function provideSite() {
  return makeEnvironmentProviders(SITE_PROVIDERS);
}
