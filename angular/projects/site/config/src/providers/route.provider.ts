import { eLayoutType, RoutesService } from '@abp/ng.core';
import { provideFlexFields } from '@dignite/ng.flex-fields';
import {
  CKEDITOR_CONFIG_CONTRIBUTORS,
  CKEDITOR_UPLOAD_PROVIDER,
  provideCKEditorFieldType,
} from '@dignite/ng.flex-fields-ckeditor';
import {
  CONTENT_FIELD_TYPE,
  FILE_FIELD_TYPE,
  SEO_FIELD_TYPE,
  SiteCKEditorUploadProvider,
  siteCKEditorConfigContributor,
} from '@dignite/ng.site';
import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  Provider,
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

const SITE_PROVIDERS: (EnvironmentProviders | Provider)[] = [
  ...SITE_ROUTE_PROVIDERS,
  // The six built-ins (Matrix/Table among them as of flex-fields 10.0.0-rc.16, via
  // provideFlexFields()'s own BUILT_IN_FIELD_TYPES - they used to be Site's own MATRIX_FIELD_TYPE/
  // TABLE_FIELD_TYPE, registered explicitly here, until flex-fields shipped them as kernel built-ins)
  // plus Site's own `Seo`, `Content` and file (GitHub issue #42) types (GitHub issue #49). `FieldTypeResolver` is root-provided and reads the registry once when first
  // injected, so this has to happen at application-config level - registering from inside the
  // lazy-loaded Site routes would come too late for a resolver already constructed.
  provideFlexFields(SEO_FIELD_TYPE, CONTENT_FIELD_TYPE, FILE_FIELD_TYPE),
  // The CKEditor field type (GitHub issue #43). Unlike the server, where DependsOn plus DI discovery is
  // enough (总体设计 §8.2), the client has no equivalent - each field type's control/config/view trio has
  // to be registered explicitly or FieldTypeResolver.get(...) throws and the content editor breaks on
  // first use, the same failure mode SEO_FIELD_TYPE's own comment warns about.
  provideCKEditorFieldType(),
  // Inline images in a CKEditor field go into Site's file library (the field's CKEditor.ImagesContainerName,
  // normally site-images) and are embedded by their public address. Without a provider the editor shows
  // no upload button at all.
  { provide: CKEDITOR_UPLOAD_PROVIDER, useExisting: SiteCKEditorUploadProvider },
  // Those addresses are stored relative (/api/site-public/files/...); the editor shows them from the
  // SiteAdmin API's host, in its editing view only - getData(), and so what is saved, stays relative.
  { provide: CKEDITOR_CONFIG_CONTRIBUTORS, multi: true, useFactory: siteCKEditorConfigContributor },
];

export function provideSite() {
  return makeEnvironmentProviders(SITE_PROVIDERS);
}
