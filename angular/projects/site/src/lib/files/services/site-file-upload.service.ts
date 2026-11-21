import { RestService } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import { map, Observable, switchMap } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import { SiteStoredFile, toStoredFile } from './site-stored-file';

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

  /**
   * The stored form of the file once it is kept: its id, its name and type as stored, and Site's address of it. It is
   * filed in `directoryId` when one is given - a directory of the container, which has to allow them.
   */
  upload(file: File, containerName: string, directoryId?: string | null): Observable<SiteStoredFile> {
    return this.files.createUploadTicket({ containerName, directoryId: directoryId ?? undefined }).pipe(
      switchMap(ticket => {
        const form = new FormData();
        form.append('file', file, file.name);

        // The ticket's address is relative to the host that serves Blob Management: the API's, in the Site host.
        return this.rest
          .request<FormData, unknown>({ method: 'POST', url: ticket.uploadUrl ?? '', body: form }, { apiName: this.files.apiName })
          .pipe(map(() => ticket.fileId ?? ''));
      }),
      switchMap(id => this.files.confirm(id)),
      map(toStoredFile),
    );
  }
}
