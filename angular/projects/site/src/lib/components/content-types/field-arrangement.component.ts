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
    /* This picker runs nzMode="multiple", so every chosen field renders as a tag - and ng-zorro
       hardcodes that tag's whole chrome: background #f5f5f5, border #f0f0f0, and rgba(0, 0, 0, 0.45)
       on the remove icon, none of it theme-aware. The tag's label, by contrast, does follow the
       host, since .field-picker-select.ant-select above sets color:inherit - so in a dark host
       the label went light while the chip stayed near-white and the x disappeared, leaving the
       selected fields barely legible. Mapped onto the same --bs-* tokens as the rest of this block. */
    :host ::ng-deep .field-picker-select .ant-select-selection-item {
      background: var(--bs-secondary-bg, #f5f5f5) !important;
      border-color: var(--bs-border-color, #f0f0f0) !important;
    }
    :host ::ng-deep .field-picker-select .ant-select-selection-item-remove {
      color: var(--bs-secondary-color, rgba(0, 0, 0, 0.45)) !important;
    }
    /* --lpx-content-bg is a FULL LeptonX token (@volosoft/ngx-lepton-x: #f0f4f7 light, #121212 dark).
       LeptonX *Lite* - @volo/ngx-lepton-x.lite, what @abp/ng.theme.lepton-x wraps, and what this app
       actually runs - never defines it: it ships 11 --lpx-* tokens and this is not one of them. The
       chain therefore fell straight through to the literal #fff and this panel stayed white in every
       theme, while .ant-select-item/-tree below kept following --bs-body-color into light-grey-on-
       white. --bs-secondary-bg is the Bootstrap 5.3 "one step off the body surface" token, defined at
       :root by every Bootstrap-based theme and redefined under [data-bs-theme=dark] (#e9ecef ->
       #343a40 in Lite), so it flips with the host. Light mode moves from pure white to #e9ecef. */
    ::ng-deep .field-picker-dropdown .ant-select-dropdown {
      background: var(--lpx-content-bg, var(--bs-secondary-bg, #fff)) !important;
    }
    ::ng-deep .field-picker-dropdown .ant-select-item-option-active:not(.ant-select-item-option-disabled) {
      background-color: var(--bs-primary) !important;
      color: var(--bs-white) !important;
    }
    /* A selected option is deliberately given no colour of its own: it reads exactly like an
       unselected one, and ant-design own checkmark (.ant-select-item-option-state) is the indicator.
       This picker is nzMode="multiple", so several options are selected at once and filling each of
       their rows competes with the hover state for attention rather than adding information.

       There used to be a rule here setting --lpx-brand with white text, and it was broken in a way
       worth recording. Only its colour half carried !important; the .ant-select-item rule below
       zeroes every option background with !important, and !important beats specificity, so the
       background never applied while the white text always did. A selected option rendered
       white-on-panel: invisible while the panel was still #fff, and merely illegible once it became
       --bs-secondary-bg. The hover rule above has !important on both halves, which is why hovering a
       selected option looked right and moving off it did not. Same fix in @dignite/ng.flex-fields
       Select control, which carried an identical copy. */
    /* !important, matching every other rule in this block - ant-design's own .ant-select-item rule
       (color: rgba(0,0,0,.85)) carries the same one-class specificity as this override, and its
       stylesheet is injected lazily when nz-select first opens, i.e. after this component's styles -
       a same-specificity tie that source order was resolving in its favor, keeping idle
       (non-hover, non-selected) options on ant-design's default dark-gray-on-transparent. */
    ::ng-deep .field-picker-dropdown .ant-select-item {
      color: var(--bs-body-color) !important;
      background-color: transparent !important;
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
