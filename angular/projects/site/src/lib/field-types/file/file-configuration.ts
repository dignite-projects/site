import { Validators } from '@angular/forms';

/**
 * The configuration keys of the file field type. **Persisted data**: every field (and every Matrix
 * block's nested configuration) stores them under these names, which date from when the field type was
 * `Dignite.Abp.FlexFields`' - the same keys as the server's `FileFieldConfigurationNames`.
 */
export const FILE_FIELD_CONFIGURATION_NAMES = {
  fileContainerName: 'FileExplorer.FileContainerName',
  uploadFileMultiple: 'FileExplorer.UploadFileMultiple',
} as const;

/** Configuration of a file field, shaped for `FormBuilder.group()`. */
export class FileFieldConfiguration {
  // No fallback container anywhere downstream (the control prompts instead of guessing), so this has to
  // be required here - the point where a misconfiguration is cheapest to catch.
  [FILE_FIELD_CONFIGURATION_NAMES.fileContainerName]: unknown = ['', [Validators.required]];

  [FILE_FIELD_CONFIGURATION_NAMES.uploadFileMultiple]: unknown = [false];
}
