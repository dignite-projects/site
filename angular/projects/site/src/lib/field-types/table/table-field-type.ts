import type { FieldTypeDefinition } from '@dignite/ng.flex-fields';
import { TableConfigComponent } from './table-config.component';
import { TableControlComponent } from './table-control.component';
import { TableViewComponent } from './table-view.component';

/** The registration key. Must equal `TableFieldType.ControlName` on the server. */
export const TABLE_FIELD_TYPE_NAME = 'Table';

/**
 * Site's 10th field type: a repeatable, homogeneous grid - one fixed column schema shared by every
 * row, unlike `Matrix`'s several independently-schemed block types (GitHub issue #49).
 *
 * **No search component.** `TableFieldType.IndexValueType` is `null` on the server - the value is a
 * list of composite row objects, not something a filter control could meaningfully query.
 */
export const TABLE_FIELD_TYPE: FieldTypeDefinition = {
  name: TABLE_FIELD_TYPE_NAME,
  displayNameKey: 'FlexFieldsSite::FieldType:Table',
  configComponent: TableConfigComponent,
  controlComponent: TableControlComponent,
  viewComponent: TableViewComponent,
};
