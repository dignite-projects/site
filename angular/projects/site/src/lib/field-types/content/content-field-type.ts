import type { FieldTypeDefinition } from '@dignite/ng.flex-fields';
import { ContentConfigComponent } from './content-config.component';
import { ContentControlComponent } from './content-control.component';
import { ContentViewComponent } from './content-view.component';

/** The registration key. Must equal `ContentFieldType.ControlName` on the server. */
export const CONTENT_FIELD_TYPE_NAME = 'Content';

/**
 * Site's 8th field type, alongside the six the flex-fields library ships and Site's own `Seo` (the
 * 7th). The server registers `ContentFieldType` through DI automatically; the client has no equivalent
 * discovery, so it is registered here and wired in through `provideSite()` (GitHub issue #49).
 *
 * Named "Content" rather than DynamicForms' own "Entry" - Site's domain has no "Entry" concept, only
 * `Content`, so the field type is named after what it actually references.
 *
 * **No search component.** `ContentFieldType.IndexValueType` is `Guid` on the server (so a future
 * filter UI is possible), but no `searchComponent` ships this round - same state CKEditor/FileExplorer
 * are currently in.
 */
export const CONTENT_FIELD_TYPE: FieldTypeDefinition = {
  name: CONTENT_FIELD_TYPE_NAME,
  displayNameKey: 'FlexFieldsSite::FieldType:Content',
  configComponent: ContentConfigComponent,
  controlComponent: ContentControlComponent,
  viewComponent: ContentViewComponent,
};
