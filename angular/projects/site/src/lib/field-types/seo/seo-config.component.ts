import { CoreModule } from '@abp/ng.core';
import { Component } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '@dignite/ng.flex-fields';
import { SeoConfiguration } from './seo-configuration';

/**
 * Designer-side editor for a `Seo` field's configuration: the two advisory character limits.
 *
 * They are thresholds the value editor counts against, not validation - see {@link SeoConfiguration}.
 */
@Component({
  selector: 'site-seo-config',
  templateUrl: './seo-config.component.html',
  imports: [CoreModule, ReactiveFormsModule],
})
export class SeoConfigComponent extends FieldTypeConfigBase {
  protected configurationDefaults(): object {
    return new SeoConfiguration();
  }
}
