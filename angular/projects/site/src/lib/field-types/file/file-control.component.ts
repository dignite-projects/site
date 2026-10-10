import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CoreModule } from '@abp/ng.core';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FieldTypeControlBase } from '@dignite/ng.flex-fields';
import { FilePickerComponent } from '../../files/components/file-picker/file-picker.component';
import { FILE_FIELD_CONFIGURATION_NAMES, FileFieldConfiguration } from './file-configuration';

/** Edits a file field's value: picks one or more files from Site's file library. */
@Component({
  selector: 'site-file-control',
  templateUrl: './file-control.component.html',
  imports: [CommonModule, CoreModule, ReactiveFormsModule, FilePickerComponent],
})
export class FileControlComponent extends FieldTypeControlBase {
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

  protected configurationDefaults(): object {
    return new FileFieldConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    const stored = Array.isArray(this.selectedValue) ? this.selectedValue : [];

    return this.fb.control(stored, validators);
  }

  onSelectedFileChange(files: unknown[]): void {
    this.fieldControl?.setValue(files);
  }
}
