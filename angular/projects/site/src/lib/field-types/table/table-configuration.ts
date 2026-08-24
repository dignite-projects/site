import { FormArray } from '@angular/forms';

/**
 * Configuration of a `Table` field, shaped for `FormBuilder.group()`. Mirrors `TableConfiguration`
 * (`src/Dignite.FlexFields.Site/Dignite/FlexFields/Site/Table/TableConfiguration.cs`).
 */
export class TableConfiguration {
  'Table.Columns': unknown = new FormArray<never>([]);
}
