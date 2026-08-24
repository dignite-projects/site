import { CoreModule } from '@abp/ng.core';
import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { AbstractControl, FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import type { FieldTypeDefinition, FlexFieldData } from '@dignite/ng.flex-fields';
import { FieldTypeConfigBase, FieldTypeResolver, FlexFieldConfigComponent } from '@dignite/ng.flex-fields';
import type { InlineFieldDefinition } from '../inline-field-definition';
import { MatrixConfiguration } from './matrix-configuration';
import { normalizeMatrixBlockTypes } from './matrix-block-type';

let nextInstanceId = 0;

/**
 * Designer-side editor for a `Matrix` field's configuration: the block-type schema. Modeled on
 * dignite-abp's `MatrixConfigComponent` (block types, each with its own field list), but built against
 * this library's own recursion mechanism rather than ported: each sub-field's own type-specific
 * configuration is delegated to a recursively-mounted `<ff-flex-field-config>`, the same generic
 * dispatch every top-level field's config editor already goes through (GitHub issue #49).
 *
 * Deliberately no drag-and-drop reordering in this first pass (`Select`'s own config editor has it, and
 * the same `@angular/cdk/drag-drop` + `moveItemInArray` pattern would apply here at two levels - block
 * types, and each block type's own fields) - add/remove is enough to be usable, reordering is a
 * reasonable follow-up rather than something this change needs to ship complete.
 */
@Component({
  selector: 'site-matrix-config',
  templateUrl: './matrix-config.component.html',
  imports: [CoreModule, CommonModule, ReactiveFormsModule, FlexFieldConfigComponent],
})
export class MatrixConfigComponent extends FieldTypeConfigBase {
  private readonly fieldTypeResolver = inject(FieldTypeResolver);

  /** Seed value for each sub-field row's own recursively-mounted config editor, keyed by its FormGroup
   * (a sub-field row carries no `configuration` control of its own until `<ff-flex-field-config>` adds
   * one, so the stored value has nowhere else to live between load and first render). */
  private readonly subFieldSeeds = new WeakMap<FormGroup, FlexFieldData | undefined>();

  readonly instanceId = `matrix-config-${nextInstanceId++}`;

  get fieldTypeOptions(): readonly FieldTypeDefinition[] {
    // A Matrix sub-field cannot itself be a Matrix - the same UI-layer-only guard DynamicForms' own
    // designer applies (nothing prevents it lower down; this is just not offering a confusing choice).
    return this.fieldTypeResolver.getAll().filter(fieldType => fieldType.name !== 'Matrix');
  }

  get blockTypes(): FormArray {
    return this.configuration.controls['Matrix.BlockTypes'] as FormArray;
  }

  protected configurationDefaults(): object {
    return new MatrixConfiguration();
  }

  protected override onConfigurationPatched(): void {
    const stored = normalizeMatrixBlockTypes(this.selectedField?.configuration['Matrix.BlockTypes']);

    stored.forEach(blockType => {
      const group = this.addBlockType();
      group.patchValue({ name: blockType.name, displayName: blockType.displayName });
      blockType.fields.forEach(field => this.addSubField(group, field));
    });
  }

  protected override onConfigurationReset(): void {
    // A field being created starts with no block types - unlike Select's one blank option, there is no
    // sensible default block type to guess at, so the admin adds them explicitly.
  }

  addBlockType(): FormGroup {
    const group = new FormGroup({
      name: new FormControl('', Validators.required),
      displayName: new FormControl('', Validators.required),
      fields: new FormArray<FormGroup>([]),
    });

    this.blockTypes.push(group);
    return group;
  }

  removeBlockType(index: number): void {
    this.blockTypes.removeAt(index);
  }

  fieldsOf(blockType: AbstractControl): FormArray {
    return (blockType as FormGroup).controls['fields'] as FormArray;
  }

  addSubField(blockType: FormGroup, seed?: InlineFieldDefinition): FormGroup {
    const group = new FormGroup({
      name: new FormControl(seed?.name ?? '', Validators.required),
      displayName: new FormControl(seed?.displayName ?? '', Validators.required),
      description: new FormControl(seed?.description ?? ''),
      fieldTypeName: new FormControl(seed?.fieldTypeName ?? '', Validators.required),
      required: new FormControl(seed?.required ?? false),
    });

    this.subFieldSeeds.set(
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

    this.fieldsOf(blockType).push(group);
    return group;
  }

  removeSubField(blockType: AbstractControl, index: number): void {
    this.fieldsOf(blockType).removeAt(index);
  }

  fieldTypeNameOf(subField: AbstractControl): string {
    return (subField as FormGroup).get('fieldTypeName')?.value ?? '';
  }

  subFieldSeedOf(subField: AbstractControl): FlexFieldData | undefined {
    return this.subFieldSeeds.get(subField as FormGroup);
  }
}
