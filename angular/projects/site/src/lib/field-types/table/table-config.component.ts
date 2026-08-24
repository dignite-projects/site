import { CoreModule } from '@abp/ng.core';
import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import type { FieldTypeDefinition, FlexFieldData } from '@dignite/ng.flex-fields';
import { FieldTypeConfigBase, FieldTypeResolver, FlexFieldConfigComponent } from '@dignite/ng.flex-fields';
import type { InlineFieldDefinition } from '../inline-field-definition';
import { normalizeInlineFieldDefinitions } from '../inline-field-definition';
import { TableConfiguration } from './table-configuration';

/**
 * Designer-side editor for a `Table` field's configuration: the one shared column schema. Simpler than
 * `MatrixConfigComponent` - a single flat list instead of block types each with their own list - but
 * built the same way: each column's own type-specific configuration is delegated to a
 * recursively-mounted `<ff-flex-field-config>` (GitHub issue #49).
 */
@Component({
  selector: 'site-table-config',
  templateUrl: './table-config.component.html',
  imports: [CoreModule, CommonModule, ReactiveFormsModule, FlexFieldConfigComponent],
})
export class TableConfigComponent extends FieldTypeConfigBase {
  private readonly fieldTypeResolver = inject(FieldTypeResolver);

  /** Seed value for each column's own recursively-mounted config editor, keyed by its FormGroup - see
   * `MatrixConfigComponent`'s identical field for why this can't just live on the FormGroup itself. */
  private readonly columnSeeds = new WeakMap<FormGroup, FlexFieldData | undefined>();

  get fieldTypeOptions(): readonly FieldTypeDefinition[] {
    // A Table column cannot itself be a Table - the same UI-layer-only guard as Matrix's own designer.
    return this.fieldTypeResolver.getAll().filter(fieldType => fieldType.name !== 'Table');
  }

  get columns(): FormArray<FormGroup> {
    return this.configuration.controls['Table.Columns'] as FormArray<FormGroup>;
  }

  protected configurationDefaults(): object {
    return new TableConfiguration();
  }

  protected override onConfigurationPatched(): void {
    const stored = normalizeInlineFieldDefinitions(this.selectedField?.configuration['Table.Columns']);
    stored.forEach(column => this.addColumn(column));
  }

  protected override onConfigurationReset(): void {
    // A field being created starts with one blank column, the same "seed one row" convention
    // `SelectConfigComponent` uses for its own option list.
    this.addColumn();
  }

  addColumn(seed?: InlineFieldDefinition): FormGroup {
    const group = new FormGroup({
      name: new FormControl(seed?.name ?? '', Validators.required),
      displayName: new FormControl(seed?.displayName ?? '', Validators.required),
      description: new FormControl(seed?.description ?? ''),
      fieldTypeName: new FormControl(seed?.fieldTypeName ?? '', Validators.required),
      required: new FormControl(seed?.required ?? false),
    });

    this.columnSeeds.set(
      group,
      seed
        ? {
            id: '',
            name: seed.name,
            displayName: seed.displayName,
            description: seed.description,
            fieldTypeName: seed.fieldTypeName,
            configuration: seed.configuration,
          }
        : undefined,
    );

    this.columns.push(group);
    return group;
  }

  removeColumn(index: number): void {
    this.columns.removeAt(index);
  }

  fieldTypeNameOf(column: FormGroup): string {
    return column.get('fieldTypeName')?.value ?? '';
  }

  columnSeedOf(column: FormGroup): FlexFieldData | undefined {
    return this.columnSeeds.get(column);
  }
}
