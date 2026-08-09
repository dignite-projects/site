import { CoreModule } from '@abp/ng.core';
import { Component, Input } from '@angular/core';
import type { FlexFieldValue } from '@dignite/ng.flex-fields';
import type { SeoFieldValue } from './seo-field-value';

/**
 * Displays the value of a `Seo` field read-only.
 *
 * In a list cell there is room for one thing, so it shows the meta title and flags `noindex` - the one
 * part of the bundle with a platform-recognized consequence.
 */
@Component({
  selector: 'site-seo-view',
  templateUrl: './seo-view.component.html',
  imports: [CoreModule],
})
export class SeoViewComponent {
  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type - always `Seo` here. */
  @Input() type?: string;

  @Input() value: unknown = '';

  get seo(): SeoFieldValue {
    return this.value && typeof this.value === 'object' ? (this.value as SeoFieldValue) : {};
  }

  get hasValue(): boolean {
    const { metaTitle, metaDescription, ogImage, noIndex } = this.seo;
    return !!(metaTitle || metaDescription || ogImage || noIndex);
  }
}
