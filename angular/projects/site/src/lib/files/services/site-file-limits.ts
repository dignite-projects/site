import { FormatFileSizePipe } from '../pipes/format-file-size.pipe';

/** What a container takes: the limits of Site's and of Blob Management's container descriptions alike. */
export interface SiteFileLimits {
  maxFileSize?: number | null;
  allowedFileExtensions?: readonly string[] | null;
}

/** A localization key and its parameters, for the toast that says why a file is not sent. */
export interface SiteFileRefusal {
  key: string;
  params: string[];
}

const fileSize = new FormatFileSizePipe();

/**
 * Why `file` would be refused by a container with `limits` - too large, or a type it does not take - or `null` when it
 * would not. Checked before a file is sent; Blob Management checks again when it arrives, so a container whose limits
 * could not be read passes everything here.
 */
export function refuseFile(file: File, limits: SiteFileLimits | null | undefined): SiteFileRefusal | null {
  const maxFileSize = limits?.maxFileSize ?? 0;
  if (maxFileSize > 0 && file.size > maxFileSize) {
    return { key: 'Site::Files:FileTooLarge', params: [file.name, fileSize.transform(maxFileSize)] };
  }

  const extensions = limits?.allowedFileExtensions ?? [];
  const name = file.name.toLowerCase();
  if (extensions.length > 0 && !extensions.some(extension => name.endsWith(extension.toLowerCase()))) {
    return { key: 'Site::Files:FileTypeNotAllowed', params: [file.name] };
  }

  return null;
}
