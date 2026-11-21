import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '@dignite/ng.flex-fields';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import { SITE_FILE_CONTAINER_NAMES } from '../../files/services/site-file-containers';
import { FILE_FIELD_CONFIGURATION_NAMES, FileFieldConfiguration, FileInputMode } from './file-configuration';

/**
 * Designer-side editor for a file field's configuration: which of Site's file containers the files are uploaded to, one
 * file or many. The containers are those the signed-in user may upload to (`FileAdminService.getContainers`); until they
 * are loaded, or when they cannot be, Site's usual ones.
 */
@Component({
  selector: 'site-file-config',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './file-config.component.html',
  imports: [CoreModule, ReactiveFormsModule],
})
export class FileConfigComponent extends FieldTypeConfigBase {
  readonly containerNames = signal<readonly string[]>(SITE_FILE_CONTAINER_NAMES);
  readonly names = FILE_FIELD_CONFIGURATION_NAMES;
  readonly inputModes = FileInputMode;

  constructor() {
    super();
    inject(FileAdminService)
      .getContainers({ skipHandleError: true })
      .subscribe({
        next: result => {
          const names = (result.items ?? []).map(container => container.name ?? '').filter(Boolean);
          if (names.length) {
            this.containerNames.set(names);
          }
        },
        error: () => undefined,
      });
  }

  protected configurationDefaults(): object {
    return new FileFieldConfiguration();
  }
}
