import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CoreModule } from '@abp/ng.core';
import { AbstractControl, FormControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FieldTypeControlBase } from '@dignite/ng.flex-fields';
import { SiteFileInputComponent } from '../../files/components/site-file-input.component';
import { SiteStoredFile } from '../../files/services/site-stored-file';
import { FILE_FIELD_CONFIGURATION_NAMES, FileFieldConfiguration, FileInputMode } from './file-configuration';

/**
 * Edits a file field's value with `site-file-input`: uploads files to the field's container and holds them as a list of
 * file objects. Values stored before Site's files moved to Blob Management - objects with `id`, `name`, `url` - show and
 * can be removed the same way.
 *
 * The input reports each upload to the page's `SiteUploadCleanup`, when it provides one, so a file uploaded here and
 * removed again before the page is saved is deleted once it is.
 */
@Component({
  selector: 'site-file-control',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './file-control.component.html',
  imports: [CommonModule, CoreModule, ReactiveFormsModule, SiteFileInputComponent],
})
export class FileControlComponent extends FieldTypeControlBase {
  get multiple(): boolean {
    return !!this.fieldValue?.field.configuration[FILE_FIELD_CONFIGURATION_NAMES.uploadFileMultiple];
  }

  /** `Upload` unless the field's configuration says `Library`: a stored value of anything else is the default. */
  get inputMode(): FileInputMode {
    return this.fieldValue?.field.configuration[FILE_FIELD_CONFIGURATION_NAMES.inputMode] === FileInputMode.Library
      ? FileInputMode.Library
      : FileInputMode.Upload;
  }

  /** Whether files are chosen from a window of those already uploaded, rather than uploaded at once. */
  get library(): boolean {
    return this.inputMode === FileInputMode.Library;
  }

  get containerName(): string {
    return (this.fieldValue?.field.configuration[FILE_FIELD_CONFIGURATION_NAMES.fileContainerName] as string) ?? '';
  }

  /** No fallback container: an unconfigured field is a misconfiguration to surface, not paper over. */
  get isContainerConfigured(): boolean {
    return this.containerName.length > 0;
  }

  /** The bound control, typed for `site-file-input`. */
  get filesControl(): FormControl<SiteStoredFile[]> {
    return this.fieldControl as FormControl<SiteStoredFile[]>;
  }

  protected configurationDefaults(): object {
    return new FileFieldConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    const stored = (Array.isArray(this.selectedValue) ? this.selectedValue : []) as SiteStoredFile[];
    return this.fb.control<SiteStoredFile[]>(stored, validators);
  }
}
