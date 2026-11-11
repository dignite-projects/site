import { Component, ChangeDetectionStrategy, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { CoreModule } from '@abp/ng.core';
import { AbstractControl, FormControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { DriveFileDto, DriveFileInputComponent } from '@dignite/ng.drive';
import { FieldTypeControlBase } from '@dignite/ng.flex-fields';
import { pairwise, startWith } from 'rxjs';
import { SiteUploadCleanup } from '../../files/services/site-upload-cleanup';
import { FILE_FIELD_CONFIGURATION_NAMES, FileFieldConfiguration } from './file-configuration';

/**
 * Edits a file field's value with the Drive's file input (`drive-file-input`): uploads files to the field's container
 * and holds them as a list of file objects. A host with Drive Pro shows its library picker in its place (the input's
 * replaceable component, `Drive.FileInputComponent`) without this changing. Values stored before Site used the Drive -
 * objects with `id`, `name`, `url` - show and can be removed the same way.
 *
 * Changes are reported to the page's `SiteUploadCleanup`, when it provides one, so a file uploaded here and removed
 * again before the page is saved is deleted from the Drive once it is.
 */
@Component({
  selector: 'site-file-control',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './file-control.component.html',
  imports: [CommonModule, CoreModule, ReactiveFormsModule, DriveFileInputComponent],
})
export class FileControlComponent extends FieldTypeControlBase {
  private readonly cleanup = inject(SiteUploadCleanup, { optional: true });
  private readonly destroyRef = inject(DestroyRef);

  get multiple(): boolean {
    return !!this.fieldValue?.field.configuration[FILE_FIELD_CONFIGURATION_NAMES.uploadFileMultiple];
  }

  get containerName(): string {
    return (this.fieldValue?.field.configuration[FILE_FIELD_CONFIGURATION_NAMES.fileContainerName] as string) ?? '';
  }

  /** No fallback container: an unconfigured field is a misconfiguration to surface, not paper over. */
  get isContainerConfigured(): boolean {
    return this.containerName.length > 0;
  }

  /** The bound control, typed for `drive-file-input`. */
  get filesControl(): FormControl<DriveFileDto[]> {
    return this.fieldControl as FormControl<DriveFileDto[]>;
  }

  protected configurationDefaults(): object {
    return new FileFieldConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    const stored = (Array.isArray(this.selectedValue) ? this.selectedValue : []) as DriveFileDto[];
    const control = this.fb.control<DriveFileDto[]>(stored, validators);

    if (this.cleanup) {
      const cleanup = this.cleanup;
      control.valueChanges
        .pipe(startWith(stored), pairwise(), takeUntilDestroyed(this.destroyRef))
        .subscribe(([previous, next]) => cleanup.changed(previous, next));
    }

    return control;
  }
}
