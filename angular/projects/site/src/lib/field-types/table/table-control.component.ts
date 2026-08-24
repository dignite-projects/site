import { CoreModule } from '@abp/ng.core';
import { CommonModule } from '@angular/common';
import {
  AbstractControl,
  FormArray,
  FormGroup,
  ReactiveFormsModule,
  ValidatorFn,
} from '@angular/forms';
import { Component } from '@angular/core';
import type { FlexFieldValue } from '@dignite/ng.flex-fields';
import { FieldTypeControlBase, FlexFieldControlComponent } from '@dignite/ng.flex-fields';
import type { InlineFieldDefinition } from '../inline-field-definition';
import { normalizeInlineFieldDefinitions } from '../inline-field-definition';
import { normalizeTableRows } from './table-row';
import { TableConfiguration } from './table-configuration';

/**
 * Edits the value of a `Table` field: a `FormArray` of rows, each a `FormGroup` holding a `values`
 * group that each column's own recursively-mounted `<ff-flex-field-control>` populates one control at a
 * time - the same mechanism `MatrixControlComponent` uses for its blocks, minus the block-type picker
 * (there is only one column schema, so a single "add row" button is enough) (GitHub issue #49).
 */
@Component({
  selector: 'site-table-control',
  templateUrl: './table-control.component.html',
  imports: [CoreModule, CommonModule, ReactiveFormsModule, FlexFieldControlComponent],
})
export class TableControlComponent extends FieldTypeControlBase {
  private readonly rowValueSeeds = new WeakMap<FormGroup, Record<string, unknown>>();

  /** `columnValueOf` is called from the template on every change-detection cycle, and
   * `<ff-flex-field-control>` only keeps its mounted child alive across a cycle if `[fields]` is
   * reference-equal to what it rendered last time - a fresh object literal per call defeats that and
   * tears down/rebuilds the recursively-mounted control (losing focus and in-progress input) on every
   * cycle, not just when the column actually changes. `columns` is only replaced when `createControl()`
   * reruns, so caching by column identity is safe here. */
  private readonly columnValueCache = new WeakMap<InlineFieldDefinition, FlexFieldValue>();

  columns: InlineFieldDefinition[] = [];

  get rows(): FormArray<FormGroup> {
    return (this.fieldControl as FormArray<FormGroup>) ?? this.fb.array<FormGroup>([]);
  }

  protected configurationDefaults(): object {
    return new TableConfiguration();
  }

  protected createControl(): AbstractControl {
    this.columns = normalizeInlineFieldDefinitions(this.fieldValue?.field.configuration['Table.Columns']);
    const stored = normalizeTableRows(this.selectedValue);

    const validators: ValidatorFn[] = [];
    if (this.fieldValue!.required) {
      validators.push(control => ((control as FormArray).length > 0 ? null : { required: true }));
    }

    const array = this.fb.array<FormGroup>([], validators);
    stored.forEach(row => array.push(this.buildRowGroup(row.values)));
    return array;
  }

  valuesGroupOf(row: AbstractControl): FormGroup {
    return (row as FormGroup).get('values') as FormGroup;
  }

  columnValueOf(column: InlineFieldDefinition): FlexFieldValue {
    let value = this.columnValueCache.get(column);
    if (!value) {
      value = {
        field: {
          id: '',
          name: column.name,
          displayName: column.displayName,
          description: column.description,
          fieldTypeName: column.fieldTypeName,
          configuration: column.configuration,
        },
        required: column.required,
        searchable: false,
      };
      this.columnValueCache.set(column, value);
    }
    return value;
  }

  selectedValueOf(row: AbstractControl, column: InlineFieldDefinition): unknown {
    return this.rowValueSeeds.get(row as FormGroup)?.[column.name];
  }

  addRow(): void {
    this.rows.push(this.buildRowGroup({}));
  }

  removeRow(index: number): void {
    this.rows.removeAt(index);
  }

  private buildRowGroup(values: Record<string, unknown>): FormGroup {
    const group = new FormGroup({ values: new FormGroup({}) });
    this.rowValueSeeds.set(group, values);
    return group;
  }
}
