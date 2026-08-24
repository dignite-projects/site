import { FormArray } from '@angular/forms';

/**
 * Configuration of a `Matrix` field, shaped for `FormBuilder.group()`. Mirrors `MatrixConfiguration`
 * (`src/Dignite.FlexFields.Site/Dignite/FlexFields/Site/Matrix/MatrixConfiguration.cs`).
 */
export class MatrixConfiguration {
  'Matrix.BlockTypes': unknown = new FormArray<never>([]);
}
