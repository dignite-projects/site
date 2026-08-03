import { eLayoutType, RoutesService } from '@abp/ng.core';
import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { eSitesRouteNames } from '../enums/route-names';

export const SITES_ROUTE_PROVIDERS = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

export function configureRoutes() {
  const routesService = inject(RoutesService);
  routesService.add([
    {
      path: '/sites',
      name: eSitesRouteNames.Sites,
      iconClass: 'fas fa-book',
      layout: eLayoutType.application,
      order: 3,
    },
  ]);
}

const SITES_PROVIDERS: EnvironmentProviders[] = [...SITES_ROUTE_PROVIDERS];

export function provideSites() {
  return makeEnvironmentProviders(SITES_PROVIDERS);
}
