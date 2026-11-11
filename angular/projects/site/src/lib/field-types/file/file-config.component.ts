import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { ReactiveFormsModule } from '@angular/forms';
import { DriveContainerService } from '@dignite/ng.drive';
import { FieldTypeConfigBase } from '@dignite/ng.flex-fields';
import { SITE_FILE_CONTAINER_NAMES } from '../../files/services/site-file-containers';
import { FILE_FIELD_CONFIGURATION_NAMES, FileFieldConfiguration } from './file-configuration';

/**
 * Designer-side editor for a file field's configuration: which Drive container the files are uploaded to, one file or
 * many. The containers are the Drive's user drives as the host declares them (`DriveContainerService`); until they are
 * loaded, or when they cannot be, Site's usual ones.
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

  constructor() {
    super();
    inject(DriveContainerService)
      .getList({ skipHandleError: true })
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
