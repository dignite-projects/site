import { Injectable, inject } from '@angular/core';
import type { CKEditorUploadProvider } from '@dignite/ng.flex-fields-ckeditor';
import { map, Observable } from 'rxjs';
import type { CreateFileInput } from '../../proxy/dignite/site/admin/files';
import { FileAdminService } from '../../proxy/dignite/site/admin/files';

/**
 * Uploads an image inserted into a CKEditor field into Site's file library and hands the editor the
 * file's public address to embed - the same `/api/site-public/files/...` address a file field stores, so
 * the image is served to visitors like any other site file. Registered as the `CKEDITOR_UPLOAD_PROVIDER`
 * by `provideSite()`; the field's `CKEditor.ImagesContainerName` names the container (normally
 * `site-images`).
 */
@Injectable({ providedIn: 'root' })
export class SiteCKEditorUploadProvider implements CKEditorUploadProvider {
  private readonly fileAdminService = inject(FileAdminService);

  upload(file: File, containerName: string): Observable<string> {
    const body = new FormData();
    body.append('file', file, file.name);

    return this.fileAdminService
      .create({
        containerName,
        directoryId: null,
        // The generated proxy sends `file` as the request body; FormData makes it multipart.
        file: body as unknown as CreateFileInput['file'],
      })
      .pipe(
        map(result => {
          if (!result?.url) {
            throw new Error('The upload succeeded but the server returned no file address.');
          }
          return result.url;
        }),
      );
  }
}
