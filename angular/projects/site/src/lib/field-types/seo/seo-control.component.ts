import { CoreModule } from '@abp/ng.core';
import { Component } from '@angular/core';
import {
  AbstractControl,
  ReactiveFormsModule,
  UntypedFormControl,
  UntypedFormGroup,
} from '@angular/forms';
import { FieldTypeControlBase } from '@dignite/ng.flex-fields';
import { SeoConfiguration } from './seo-configuration';
import type { SeoFieldValue } from './seo-field-value';

/**
 * Edits the value of a `Seo` field.
 *
 * Unlike every built-in field type, this binds a **`FormGroup`** rather than a single control: the
 * value is a composite object, and the group's shape is exactly `SeoFieldValue`, so what the form
 * produces round-trips back to the server unchanged.
 *
 * **No sub-field is individually required, even when the field usage is.** The server's
 * `SeoFieldType.Validate` only rejects a *missing bundle*; SEO fields are supplementary and the
 * platform falls back to defaults for whatever is left blank (总体设计 §5.3). Since a `FormGroup`
 * always produces an object, a required Seo field is satisfied by being rendered at all - adding
 * `Validators.required` to the parts would enforce something the server does not.
 */
@Component({
  selector: 'site-seo-control',
  templateUrl: './seo-control.component.html',
  imports: [CoreModule, ReactiveFormsModule],
})
export class SeoControlComponent extends FieldTypeControlBase {
  protected configurationDefaults(): object {
    return new SeoConfiguration();
  }

  /**
   * Untyped on purpose. `FieldTypeControlBase.createControl` is declared to return
   * `AbstractControl<any, any, any>`, and a typed `FormGroup<{...}>` is not assignable to it - typed
   * forms fix `TRawValue` to the group's own shape. Every built-in field type returns a single
   * control, so this is the first one to hit it.
   */
  protected createControl(): AbstractControl {
    const value = this.currentValue();

    return new UntypedFormGroup({
      metaTitle: new UntypedFormControl(value.metaTitle ?? ''),
      metaDescription: new UntypedFormControl(value.metaDescription ?? ''),
      ogImage: new UntypedFormControl(value.ogImage ?? ''),
      noIndex: new UntypedFormControl(value.noIndex ?? false),
    });
  }

  /** The bound group, for the template's counters. */
  get seoGroup(): UntypedFormGroup | null {
    return this.fieldControl as UntypedFormGroup | null;
  }

  get metaTitleLength(): number {
    return String(this.seoGroup?.get('metaTitle')?.value ?? '').length;
  }

  get metaDescriptionLength(): number {
    return String(this.seoGroup?.get('metaDescription')?.value ?? '').length;
  }

  get metaTitleCharLimit(): number {
    return Number(this.fieldValue?.field.configuration['Seo.MetaTitleCharLimit'] ?? 60);
  }

  get metaDescriptionCharLimit(): number {
    return Number(this.fieldValue?.field.configuration['Seo.MetaDescriptionCharLimit'] ?? 160);
  }

  /**
   * A fresh value is a live object; one that has round-tripped through storage arrives as parsed JSON.
   * Anything else - the base class seeds `''` when there is no value - means "nothing stored yet".
   */
  private currentValue(): Partial<SeoFieldValue> {
    const value = this.selectedValue;
    return value && typeof value === 'object' ? (value as Partial<SeoFieldValue>) : {};
  }
}
