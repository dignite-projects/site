import { Injectable, inject } from '@angular/core';
import type { CKEditorUploadProvider } from '@dignite/ng.flex-fields-ckeditor';
import { map, Observable, tap } from 'rxjs';
import { SiteFileUploadService } from './site-file-upload.service';
import { SiteFileUrlService } from './site-file-url.service';
import { SiteUploadCleanup } from './site-upload-cleanup';

/**
 * Uploads an image inserted into a CKEditor field to one of Site's file containers (`SiteFileUploadService`) and hands
 * the editor the image's address to embed - Blob Management's address of it (`/blobs/...`), so the image is served
 * to visitors like any other site file. Registered as the `CKEDITOR_UPLOAD_PROVIDER` by `provideSite()`; the field's
 * `CKEditor.ImagesContainerName` names the container (normally `images`). The user needs the permission that uploads
 * to it - editing contents, for `images`.
 *
 * The editor gets the address on the host this UI reaches it at: its model holds absolute addresses, so it can show the
 * image and read its size (`width`/`height`); `siteCKEditorConfigContributor` turns it back into the relative address
 * when the editor's data is read and saved.
 *
 * Each upload is reported to the page's `SiteUploadCleanup`, so an image the saved text does not hold, or that an unsaved
 * edit uploaded, is deleted like a file of a `site-file-input`. The instance `provideSite()` registers is the root's and
 * finds no page; a page that saves the editor's text lists this class in its own `providers`, with the
 * `CKEDITOR_UPLOAD_PROVIDER` token pointing at it, so that its instance sees the page's cleanup.
 */
@Injectable({ providedIn: 'root' })
export class SiteCKEditorUploadProvider implements CKEditorUploadProvider {
  private readonly uploads = inject(SiteFileUploadService);
  private readonly fileUrls = inject(SiteFileUrlService);
  private readonly cleanup = inject(SiteUploadCleanup, { optional: true });

  upload(file: File, containerName: string): Observable<string> {
    return this.uploads.upload(file, containerName).pipe(
      tap(result => this.cleanup?.uploaded(result)),
      map(result => {
        if (!result.url) {
          throw new Error('The upload succeeded but the file has no public address.');
        }
        return this.fileUrls.toDisplay(result.url);
      }),
    );
  }
}
