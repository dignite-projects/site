import { eLayoutType, RoutesService } from '@abp/ng.core';
import { provideFlexFields } from '@dignite/ng.flex-fields';
import { provideCKEditorFieldType } from '@dignite/ng.flex-fields-ckeditor';
import { provideFileExplorerFieldType } from '@dignite/ng.flex-fields-file-explorer';
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
      iconClass: 'fas fa-globe',
      layout: eLayoutType.application,
      order: 3,
      requiredPolicy:
        'SiteAdmin.Pages || SiteAdmin.ContentTypes || SiteAdmin.Fields || SiteAdmin.Contents',
    },
    {
      path: '/site/contents',
      name: eSiteRouteNames.Contents,
      parentName: eSiteRouteNames.Site,
      iconClass: 'fas fa-file-lines',
      layout: eLayoutType.application,
      order: 1,
      requiredPolicy: 'SiteAdmin.Contents',
    },
    {
      path: '/site/pages',
      name: eSiteRouteNames.Pages,
      parentName: eSiteRouteNames.Site,
      iconClass: 'fas fa-file-code',
      layout: eLayoutType.application,
      order: 2,
      requiredPolicy: 'SiteAdmin.Pages',
    },
    {
      path: '/site/fields',
      name: eSiteRouteNames.Fields,
      parentName: eSiteRouteNames.Site,
      iconClass: 'fas fa-list-check',
      layout: eLayoutType.application,
      order: 3,
      requiredPolicy: 'SiteAdmin.Fields',
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
  // CKEditor (GitHub issue #43) and FileExplorer (#42) field types. Unlike the server, where DependsOn
  // plus DI discovery is enough (总体设计 §8.2), the client has no equivalent - each field type's
  // control/config/view trio has to be registered explicitly or FieldTypeResolver.get(...) throws and
  // the content editor breaks on first use, the same failure mode SEO_FIELD_TYPE's own comment warns
  // about. This was missing entirely until now, which is why a content type pulling in a CKEditor field
  // failed to render at all - not just the field, the page around it.
  provideCKEditorFieldType(),
  provideFileExplorerFieldType(),
];

export function provideSite() {
  return makeEnvironmentProviders(SITE_PROVIDERS);
}
