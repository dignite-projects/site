import { CoreModule, LocalizationService } from '@abp/ng.core';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, Input, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FieldTypeResolver } from '@dignite/ng.flex-fields';
import type { FieldTypeDefinition } from '@dignite/ng.flex-fields';
import { NzSelectModule } from 'ng-zorro-antd/select';
import type { NzSelectOptionInterface } from 'ng-zorro-antd/select';
import type { ContentTypeFieldDto } from '../../proxy/dignite/site/content-types/models';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';

/**
 * Arranges a content type's fields: which ones it pulls in from the library, in what order, and how
 * each is used.
 *
 * **The arrangement is edited in place and saved whole.** `UpdateContentTypeDto.Fields` replaces the
 * entire list server-side - any usage flag left out of the payload resets to its default. So the
 * parent holds the complete `ContentTypeFieldDto[]`, this component mutates that same array, and the
 * parent sends all of it. Never send a delta.
 *
 * `ContentTypeFieldDto` carries only `fieldId`; names, types and descriptions come from
 * {@link fieldsById}, joined here.
 */
@Component({
  selector: 'site-field-arrangement',
  templateUrl: './field-arrangement.component.html',
  imports: [CoreModule, FormsModule, DragDropModule, NzSelectModule],
  styles: `
    /* Dresses nz-select up as a Bootstrap .form-select, matching flex-fields' ff-select-control. */
    :host ::ng-deep .field-picker-select {
      width: 100%;
    }
    :host ::ng-deep .field-picker-select.ant-select {
      color: inherit !important;
    }
    :host ::ng-deep .field-picker-select .ant-select-selector {
      background: transparent !important;
      border: none !important;
      padding: 0.475rem 1.25rem !important;
      box-shadow: none !important;
      height: auto !important;
    }
    :host ::ng-deep .field-picker-select .ant-select-arrow {
      display: none !important;
    }
    :host ::ng-deep .field-picker-select .ant-select-selection-placeholder {
      color: inherit !important;
    }
    :host ::ng-deep .field-picker-select.form-select {
      padding: 0 !important;
    }
    ::ng-deep .field-picker-dropdown .ant-select-dropdown {
      background: var(--lpx-content-bg, #fff) !important;
    }
    ::ng-deep .field-picker-dropdown .ant-select-item-option-active:not(.ant-select-item-option-disabled) {
      background-color: var(--bs-primary) !important;
      color: var(--bs-white) !important;
    }
    ::ng-deep .field-picker-dropdown .ant-select-item-option-selected:not(.ant-select-item-option-disabled) {
      background-color: var(--lpx-brand);
      color: var(--bs-white) !important;
    }
    ::ng-deep .field-picker-dropdown .ant-select-item {
      color: var(--bs-body-color);
      background-color: transparent;
    }
  `,
})
export class FieldArrangementComponent {
  private readonly fieldTypeResolver = inject(FieldTypeResolver);
  private readonly localization = inject(LocalizationService);

  readonly fieldTypes: readonly FieldTypeDefinition[] = this.fieldTypeResolver.getAll();

  /** The live arrangement. Mutated in place - the parent reads it back at save time. */
  @Input() arrangement: ContentTypeFieldDto[] = [];

  /** The whole field library, keyed by id. */
  @Input() fieldsById = new Map<string, FieldDto>();

  /** Whether a field type has a query index at all, keyed by field type name. */
  @Input() indexableByFieldType = new Map<string, boolean>();

  /** Field ids chosen in the "add field" picker, before they're committed via {@link addFields}. */
  fieldsToAdd: string[] = [];

  fieldOf(entry: ContentTypeFieldDto): FieldDto | undefined {
    return entry.fieldId ? this.fieldsById.get(entry.fieldId) : undefined;
  }

  labelOf(entry: ContentTypeFieldDto): string {
    return entry.displayName || this.fieldOf(entry)?.displayName || this.fieldOf(entry)?.name || '';
  }

  /** Localization key for a field type's display name, for use with `| abpLocalization`. */
  displayNameKeyOf(fieldTypeName?: string): string {
    return (
      this.fieldTypes.find(type => type.name === fieldTypeName)?.displayNameKey ??
      fieldTypeName ??
      ''
    );
  }

  /** Library fields not already in the arrangement. */
  get addableFields(): FieldDto[] {
    const used = new Set(this.arrangement.map(entry => entry.fieldId));
    return [...this.fieldsById.values()].filter(field => !used.has(field.id));
  }

  private addableFieldOptionsCache?: { key: string; options: NzSelectOptionInterface[] };

  /**
   * {@link addableFields} as `nz-select` options. Labels are resolved here, not in the template -
   * `nzOptions` is consumed internally by ng-zorro rather than rendered through Angular's pipe on each
   * row, so the localized field type name has to already be a plain string by the time it's bound.
   *
   * **Cached against a cheap key, not recomputed every read.** `nzOptions` expects a stable array
   * reference; `arrangement` is mutated in place ({@link arrangement}'s own doc), so its identity never
   * changes when a field is added or removed, and a plain `.map()` here would hand `nz-select` a fresh
   * array and fresh option objects on every change-detection cycle - including the ones firing while the
   * dropdown is open (hover, search keystrokes). `ff-select-control` in flex-fields guards the same
   * getter the same way, for the same reason.
   */
  get addableFieldOptions(): NzSelectOptionInterface[] {
    const key = `${this.fieldsById.size}:${this.arrangement.length}`;
    if (this.addableFieldOptionsCache?.key !== key) {
      this.addableFieldOptionsCache = {
        key,
        options: this.addableFields.map(field => ({
          label: `${field.displayName} (${this.localization.instant(this.displayNameKeyOf(field.fieldTypeName))})`,
          value: field.id,
        })),
      };
    }
    return this.addableFieldOptionsCache.options;
  }

  /**
   * A field whose type has no query-index slot can never be searched, whatever this flag says -
   * `FlexFieldIndexManagerBase.GetIndexableFieldsAsync` skips it. Lock the checkbox off and say why,
   * rather than letting someone set something that silently does nothing.
   */
  isSearchableSupported(entry: ContentTypeFieldDto): boolean {
    const fieldTypeName = this.fieldOf(entry)?.fieldTypeName;
    return !fieldTypeName || this.indexableByFieldType.get(fieldTypeName) !== false;
  }

  addFields(): void {
    if (this.fieldsToAdd.length === 0) {
      return;
    }

    this.fieldsToAdd.forEach(fieldId => {
      this.arrangement.push({
        fieldId,
        required: false,
        searchable: false,
        showInList: false,
        displayName: null,
        order: this.arrangement.length,
      });
    });

    this.fieldsToAdd = [];
    this.reindex();
  }

  removeField(index: number): void {
    this.arrangement.splice(index, 1);
    this.reindex();
  }

  drop(event: CdkDragDrop<ContentTypeFieldDto[]>): void {
    moveItemInArray(this.arrangement, event.previousIndex, event.currentIndex);
    this.reindex();
  }

  onSearchableChange(entry: ContentTypeFieldDto): void {
    if (!this.isSearchableSupported(entry)) {
      entry.searchable = false;
    }
  }

  /** `order` is the server's sort key, so it has to follow the visual order after any change. */
  private reindex(): void {
    this.arrangement.forEach((entry, index) => (entry.order = index));
  }
}
