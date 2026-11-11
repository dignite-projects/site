import { Injectable, inject } from '@angular/core';
import type { CKEditorUploadProvider } from '@dignite/ng.flex-fields-ckeditor';
import { DriveFileService } from '@dignite/ng.drive';
import { map, Observable } from 'rxjs';
import { SiteFileUrlService } from './site-file-url.service';

/**
 * Uploads an image inserted into a CKEditor field to the Drive and hands the editor the image's address to embed -
 * Site's address of it (`/api/site-public/files/...`), which `normalizeFileUrl` makes of the address the Drive returns,
 * so the image is served to visitors like any other site file. Registered as the `CKEDITOR_UPLOAD_PROVIDER` by
 * `provideSite()`; the field's `CKEditor.ImagesContainerName` names the Drive container (normally `images`). The user
 * needs the Drive's `Drive.Files.Upload`.
 *
 * The editor gets the address on the `SiteAdmin` API's host: its model holds absolute addresses, so it can show the
 * image and read its size (`width`/`height`); `siteCKEditorConfigContributor` turns it back into the relative address
 * when the editor's data is read and saved.
 */
@Injectable({ providedIn: 'root' })
export class SiteCKEditorUploadProvider implements CKEditorUploadProvider {
  private readonly fileService = inject(DriveFileService);
  private readonly fileUrls = inject(SiteFileUrlService);

  upload(file: File, containerName: string): Observable<string> {
    const body = new FormData();
    body.append('containerName', containerName);
    body.append('file', file, file.name);

    return this.fileService.create(body).pipe(
      map(result => {
        if (!result?.url) {
          throw new Error('The upload succeeded but the Drive returned no public address: is the container read publicly?');
        }
        return this.fileUrls.toDisplay(result.url);
      }),
    );
  }
}
