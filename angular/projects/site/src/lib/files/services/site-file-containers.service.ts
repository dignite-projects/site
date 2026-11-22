import { Injectable, inject } from '@angular/core';
import { catchError, map, Observable, shareReplay, throwError } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import type { SiteFileContainerDto } from '../../proxy/dignite/site/admin/files/models';

/**
 * The containers the signed-in user may upload to, with their limits (`GET api/site-admin/files/containers`), asked
 * for once and shared: every `site-file-input` of a form and the file field's configuration read the same answer, where
 * each used to ask on its own - and the server asks Blob Management about each of Site's containers per call. What the
 * user may upload to changes with their permissions, which take effect for the admin UI when it starts again, as ABP's
 * application configuration does.
 *
 * A failed request is not kept: the next caller asks again. ABP's error handler is skipped - a caller without the limits
 * offers an upload Blob Management still checks.
 */
@Injectable({ providedIn: 'root' })
export class SiteFileContainersService {
  private readonly files = inject(FileAdminService);
  private containers$?: Observable<SiteFileContainerDto[]>;

  list(): Observable<SiteFileContainerDto[]> {
    if (!this.containers$) {
      this.containers$ = this.files.getContainers({ skipHandleError: true }).pipe(
        map(result => result.items ?? []),
        catchError(error => {
          this.containers$ = undefined;
          return throwError(() => error);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }

    return this.containers$;
  }
}
