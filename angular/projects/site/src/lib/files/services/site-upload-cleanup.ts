import { ConfigStateService } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import { DriveFileDto, DriveFileService } from '@dignite/ng.drive';
import { catchError, forkJoin, map, Observable, of } from 'rxjs';

/**
 * The files uploaded while one form is edited, so that those taken out of it again before it is saved are deleted from
 * the Drive once it is saved. Removing a file from a `drive-file-input` only takes it out of the value: the form may
 * never be saved, and the value it replaces may still point at the file. A file this user uploaded during this edit and
 * that the saved form does not hold is in neither place, so deleting it loses nothing. Files that were already in the
 * form, and files picked rather than uploaded, are never deleted here.
 *
 * Provided by the page that saves the form (the content editor, the SEO settings); the file inputs inside it report
 * their changes with `changed`, and the page calls `saved` with what it saved. A file input outside such a page finds no
 * provider and reports nothing.
 *
 * "Uploaded during this edit" is read from the file itself: created by the current user, no earlier than the page was
 * opened. A browser clock ahead of the server's can only make a fresh upload look older - kept, never deleted wrongly.
 */
@Injectable()
export class SiteUploadCleanup {
  private readonly fileService = inject(DriveFileService);
  private readonly config = inject(ConfigStateService);
  private readonly openedAt = Date.now();
  private readonly uploaded = new Map<string, DriveFileDto>();

  /** A file input's value changed from `previous` to `next`. */
  changed(previous: readonly DriveFileDto[] | null | undefined, next: readonly DriveFileDto[] | null | undefined): void {
    const before = new Set((previous ?? []).map(file => file.id));
    for (const file of next ?? []) {
      if (file.id && !before.has(file.id) && this.isUploadOfThisEdit(file)) {
        this.uploaded.set(file.id, file);
      }
    }
  }

  /**
   * The form was saved as `savedValue`: deletes every file uploaded during this edit that the saved value no longer
   * names (by id, or by the address it is stored under), and starts over. Best effort: a file that cannot be deleted is
   * left in the Drive, and the save is not affected.
   */
  saved(savedValue: unknown): Observable<void> {
    const saved = JSON.stringify(savedValue ?? null);
    const unused = [...this.uploaded.values()].filter(file => !this.isNamedIn(saved, file));
    this.uploaded.clear();

    if (!unused.length) {
      return of(undefined);
    }

    return forkJoin(
      unused.map(file => this.fileService.delete(file.id!, { skipHandleError: true }).pipe(catchError(() => of(undefined)))),
    ).pipe(map(() => undefined));
  }

  private isUploadOfThisEdit(file: DriveFileDto): boolean {
    const userId = this.config.getOne('currentUser')?.id;
    const created = file.creationTime ? new Date(file.creationTime).getTime() : NaN;
    return !!userId && file.creatorId === userId && created >= this.openedAt;
  }

  /** Whether the serialized value names `file` - its id, or the BLOB name its address ends in. */
  private isNamedIn(saved: string, file: DriveFileDto): boolean {
    if (file.id && saved.includes(file.id)) {
      return true;
    }

    const path = (file.url ?? '').split('?')[0];
    const blobName = path.substring(path.lastIndexOf('/') + 1);
    return !!blobName && saved.includes(blobName);
  }
}
