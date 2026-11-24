import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { Injectable, inject } from '@angular/core';
import { catchError, concatMap, EMPTY, finalize, from, map, Observable, switchMap } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import { refuseFile, SiteFileLimits } from './site-file-limits';
import { SiteStoredFile, toStoredFile } from './site-stored-file';

/** The ABP API name of Blob Management in the environment's `apis`, as Blob Management's own UI uses it. */
export const BLOB_MANAGEMENT_API_NAME = 'BlobManagement';

/**
 * Uploads one file to one of Site's containers, in Blob Management's three steps: a one-use ticket from Site's
 * administration API (which applies Site's rules for the container), the file posted to the ticket's address - straight
 * to Blob Management, which checks it against the container's limits and stores it - and the confirmation that files it
 * and makes it readable by the site's visitors. The bytes never pass through Site's own endpoints.
 *
 * A failure at any step is shown by ABP's error handler, and surfaces here as an error of the observable. A file
 * uploaded and not confirmed is deleted by Blob Management after a day.
 */
@Injectable({ providedIn: 'root' })
export class SiteFileUploadService {
  private readonly files = inject(FileAdminService);
  private readonly rest = inject(RestService);
  private readonly toaster = inject(ToasterService);

  /**
   * The stored form of the file once it is kept: its id, its name and type as stored, and Site's address of it. It is
   * filed in `directoryId` when one is given - a directory of the container, which has to allow them.
   */
  upload(file: File, containerName: string, directoryId?: string | null): Observable<SiteStoredFile> {
    return this.files.createUploadTicket({ containerName, directoryId: directoryId ?? undefined }).pipe(
      switchMap(ticket => {
        const form = new FormData();
        form.append('file', file, file.name);

        // The ticket's address is relative to the host that serves Blob Management: the `BlobManagement` API's. ABP takes
        // `apis.default` when the environment does not list it, which is right when Blob Management runs in the host of
        // the `SiteAdmin` API. No permission is checked on this request: the ticket, which Site issued only after its own
        // check, is what Blob Management accepts the file on.
        return this.rest
          .request<FormData, unknown>({ method: 'POST', url: ticket.uploadUrl ?? '', body: form }, { apiName: BLOB_MANAGEMENT_API_NAME })
          .pipe(map(() => ticket.fileId ?? ''));
      }),
      switchMap(id => this.files.confirm(id)),
      map(toStoredFile),
    );
  }

  /**
   * Uploads `files` one after another, leaving out - each with a toast saying why - those a container with `limits` would
   * refuse. A file that fails does not stop the others: ABP's error handler has shown each failure, and one toast counts
   * them at the end. Emits each file as soon as it is kept.
   */
  uploadAll(
    files: readonly File[],
    containerName: string,
    limits: SiteFileLimits | null | undefined,
    directoryId?: string | null,
  ): Observable<SiteStoredFile> {
    const accepted = files.filter(file => {
      const refusal = refuseFile(file, limits);
      if (refusal) {
        this.toaster.error(refusal.key, '', { messageLocalizationParams: refusal.params });
      }
      return !refusal;
    });

    let failed = 0;
    return from(accepted).pipe(
      concatMap(file =>
        this.upload(file, containerName, directoryId).pipe(
          catchError(() => {
            failed++;
            return EMPTY;
          }),
        ),
      ),
      finalize(() => {
        if (failed > 0) {
          this.toaster.error('Site::Files:UploadFailed', '', {
            messageLocalizationParams: [String(failed), String(accepted.length)],
          });
        }
      }),
    );
  }
}
