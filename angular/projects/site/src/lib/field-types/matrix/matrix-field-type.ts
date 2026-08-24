import type { FieldTypeDefinition } from '@dignite/ng.flex-fields';
import { MatrixConfigComponent } from './matrix-config.component';
import { MatrixControlComponent } from './matrix-control.component';
import { MatrixViewComponent } from './matrix-view.component';

/** The registration key. Must equal `MatrixFieldType.ControlName` on the server. */
export const MATRIX_FIELD_TYPE_NAME = 'Matrix';

/**
 * Site's 9th field type - a repeatable list of polymorphic "blocks": the admin declares one or more
 * named block types up front, each with its own sub-fields, and a value is a list of block instances
 * (GitHub issue #49).
 *
 * **No search component.** `MatrixFieldType.IndexValueType` is `null` on the server - the value is a
 * list of composite objects, not something a filter control could meaningfully query.
 */
export const MATRIX_FIELD_TYPE: FieldTypeDefinition = {
  name: MATRIX_FIELD_TYPE_NAME,
  displayNameKey: 'FlexFieldsSite::FieldType:Matrix',
  configComponent: MatrixConfigComponent,
  controlComponent: MatrixControlComponent,
  viewComponent: MatrixViewComponent,
};
