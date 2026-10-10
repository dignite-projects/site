import { Component } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '@dignite/ng.flex-fields';
import { SITE_FILE_CONTAINER_NAMES } from '../../files/services/site-file-containers';
import { FILE_FIELD_CONFIGURATION_NAMES, FileFieldConfiguration } from './file-configuration';

/** Designer-side editor for a file field's configuration: which of Site's containers, one file or many. */
@Component({
  selector: 'site-file-config',
  templateUrl: './file-config.component.html',
  imports: [CoreModule, ReactiveFormsModule],
})
export class FileConfigComponent extends FieldTypeConfigBase {
  readonly containerNames = SITE_FILE_CONTAINER_NAMES;
  readonly names = FILE_FIELD_CONFIGURATION_NAMES;

  protected configurationDefaults(): object {
    return new FileFieldConfiguration();
  }
}
