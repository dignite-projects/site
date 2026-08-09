import type { FieldTypeDefinition } from '@dignite/ng.flex-fields';
import { SeoConfigComponent } from './seo-config.component';
import { SeoControlComponent } from './seo-control.component';
import { SeoViewComponent } from './seo-view.component';

/** The registration key. Must equal `SeoFieldNames.FieldTypeName` on the server. */
export const SEO_FIELD_TYPE_NAME = 'Seo';

/**
 * Site's seventh field type, alongside the six the flex-fields library ships. The server registers
 * `SeoFieldType` through DI automatically; the client has no equivalent discovery, so it is registered
 * here and wired in through `provideSite()`.
 *
 * This is not optional wiring: `seo` is a platform-preset seeded field, so a fresh install already has
 * one. Without this registration `FieldTypeResolver.get('Seo')` throws and both the field designer and
 * the content editor break on first use.
 *
 * **No search component.** `SeoFieldType.IndexValueType` is null - the value is a composite object with
 * no typed index column to project into - so a `Seo` field can never be searched and must not offer a
 * filter control.
 */
export const SEO_FIELD_TYPE: FieldTypeDefinition = {
  name: SEO_FIELD_TYPE_NAME,
  displayNameKey: 'Site::FieldType:Seo',
  configComponent: SeoConfigComponent,
  controlComponent: SeoControlComponent,
  viewComponent: SeoViewComponent,
};
