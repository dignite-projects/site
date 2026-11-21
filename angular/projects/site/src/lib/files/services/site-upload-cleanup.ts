import { Injectable, inject } from '@angular/core';
import { catchError, forkJoin, map, Observable, of } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import { SiteStoredFile } from './site-stored-file';

/**
 * The files uploaded while one form is edited, so that those the saved form does not hold are deleted once it is
 * saved. Removing a file from a `site-file-input` only takes it out of the value: the form may never be saved, and the
 * value it replaces may still point at the file. A file uploaded during this edit and that the saved form does not
 * name is in neither place, so deleting it loses nothing. Files that were already in the form are never deleted here.
 *
 * Provided by the page that saves the form (the content editor, the SEO settings); the `site-file-input`s inside it
 * report each upload with `uploaded`, and the page calls `saved` with what it saved. A file input outside such a page
 * finds no provider and reports nothing.
 */
@Injectable()
export class SiteUploadCleanup {
  private readonly fileService = inject(FileAdminService);
  private readonly uploads = new Map<string, SiteStoredFile>();

  /** A file was uploaded and confirmed during this edit. */
  uploaded(file: SiteStoredFile): void {
    if (file.id) {
      this.uploads.set(file.id, file);
    }
  }

  /**
   * The form was saved as `savedValue`: deletes every file uploaded during this edit that the saved value no longer
   * names (by id, or by the address it is stored under), and starts over. Best effort: a file that cannot be deleted
   * stays, and the save is not affected.
   */
  saved(savedValue: unknown): Observable<void> {
    const saved = JSON.stringify(savedValue ?? null);
    const unused = [...this.uploads.values()].filter(file => !this.isNamedIn(saved, file));
    this.uploads.clear();

    if (!unused.length) {
      return of(undefined);
    }

    return forkJoin(
      unused.map(file => this.fileService.delete(file.id, { skipHandleError: true }).pipe(catchError(() => of(undefined)))),
    ).pipe(map(() => undefined));
  }

  /** Whether the serialized value names `file` - its id, or the BLOB name its address ends in. */
  private isNamedIn(saved: string, file: SiteStoredFile): boolean {
    if (file.id && saved.includes(file.id)) {
      return true;
    }

    const path = (file.url ?? '').split('?')[0];
    const blobName = path.substring(path.lastIndexOf('/') + 1);
    return !!blobName && saved.includes(blobName);
  }
}
