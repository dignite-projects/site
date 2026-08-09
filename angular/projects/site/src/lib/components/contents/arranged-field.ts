import type { FlexFieldData, FlexFieldValue } from '@dignite/ng.flex-fields';

/**
 * One field as a content type actually uses it: the library definition joined with that content type's
 * usage flags.
 *
 * `ContentTypeFieldDto` carries only `fieldId` and the flags; the name, type and configuration live on
 * `FieldDto`. Neither alone is enough to render anything, so they are joined once per page load.
 */
export interface ArrangedField {
  fieldId: string;
  /** The key this field's value is stored under in `ContentDto.fieldValues`. */
  name: string;
  displayName: string;
  fieldTypeName: string;
  required: boolean;
  searchable: boolean;
  showInList: boolean;
  order: number;
  /** What the `ff-flex-field-*` components take. */
  fieldData: FlexFieldData;
}

/**
 * A column in the content list.
 *
 * Columns are the **union** across every content type on the page, deduped by field: a page can hold
 * several content types and the list shows them together whenever no content type filter is set.
 * `contentTypeIds` is which of them actually use this field - a row whose type is not in that set has
 * no value for the column and renders a placeholder.
 *
 * Deduping by field rather than by (content type, field) pair is deliberate: two content types that
 * both pull in `title` share one column, because it is the same field. Keying columns by the pair - as
 * Dignite.Cms does - would show `title` twice with each row filled in only one of them.
 */
export interface FieldColumn {
  fieldId: string;
  name: string;
  displayName: string;
  fieldTypeName: string;
  contentTypeIds: Set<string>;
}

/** A stable `FlexFieldValue` for one row's cell, or `undefined` when the column does not apply. */
export type RowFieldValues = Map<string, FlexFieldValue>;
