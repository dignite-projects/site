import type { FieldTypeDefinition } from '@dignite/ng.flex-fields';
import { FileConfigComponent } from './file-config.component';
import { FileControlComponent } from './file-control.component';
import { FileViewComponent } from './file-view.component';

/**
 * The registration key. **Persisted data** (every field definition of this type, Matrix block
 * configurations included) - unchanged from when the field type was `Dignite.Abp.FlexFields`'; must equal
 * the server's `FileFieldType.ControlName`.
 */
export const FILE_FIELD_TYPE_NAME = 'FileExplorer';

/**
 * The file field type: picks one or more files from Site's file library. Registered by `provideSite()`
 * like `Seo` and `Content` - the client has no equivalent of the server's DI discovery.
 *
 * **No search component.** `FileFieldType.IndexValueType` is null - the value is an array of file
 * objects with no typed index column to project into.
 */
export const FILE_FIELD_TYPE: FieldTypeDefinition = {
  name: FILE_FIELD_TYPE_NAME,
  displayNameKey: 'FlexFieldsSite::FieldType:File',
  configComponent: FileConfigComponent,
  controlComponent: FileControlComponent,
  viewComponent: FileViewComponent,
};
