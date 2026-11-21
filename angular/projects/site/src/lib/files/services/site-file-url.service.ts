import { Injectable, inject } from '@angular/core';
import { EnvironmentService, SessionStateService } from '@abp/ng.core';
import { absoluteFileUrl, relativeFileUrl, SiteFileBases } from './site-file-url';

/**
 * Where this admin UI shows Site's files from. A stored address is relative - Blob Management's `/blobs/...`, or an
 * earlier `/api/site-public/files/...` - and this UI is served from an origin of its own, so the previews
 * (`site-file-preview`: a file field, the SEO share image, the file picker) put the host in front of it:
 * `environment.apis.BlobManagement.url` for Blob Management's address, and `environment.apis.SiteAdmin.url` for an
 * earlier one, which Site's API redirects. ABP takes `apis.default` for an API the environment does not list, which is
 * right when Blob Management runs in the host of the `SiteAdmin` API. The CKEditor field does the same inside the editor
 * (`siteCKEditorConfigContributor`). Nothing that is saved carries the host.
 */
@Injectable({ providedIn: 'root' })
export class SiteFileUrlService {
  private readonly environment = inject(EnvironmentService);
  private readonly session = inject(SessionStateService);

  /** The base URLs of the two APIs, with no trailing slash; empty when the UI shares their origin. */
  get bases(): SiteFileBases {
    // getApiUrl itself, guarded: it dereferences `apis` unchecked, and an environment without them (a test bed, say)
    // means there is no other origin to show files from.
    const apis = this.environment.getEnvironment()?.apis;
    const url = (name: string) => (apis ? (this.environment.getApiUrl(name) ?? '').replace(/\/+$/, '') : '');
    return { blobManagement: url('BlobManagement'), site: url('SiteAdmin') };
  }

  /** The signed-in tenant's id; `null` on the host. */
  get tenantId(): string | null {
    return this.session.getTenant()?.id ?? null;
  }

  /** Whether a stored address loads as it is here: the UI shares both APIs' origin, on the host. */
  get loadsStoredAddresses(): boolean {
    const bases = this.bases;
    return !bases.blobManagement && !bases.site && !this.tenantId;
  }

  /** A stored file address as this UI loads it - see `absoluteFileUrl`. */
  toDisplay(url: string | null | undefined): string {
    return absoluteFileUrl(url, this.bases, this.tenantId);
  }

  /** The inverse of `toDisplay`: an address as it is stored - see `relativeFileUrl`. */
  toStored(url: string | null | undefined): string {
    return relativeFileUrl(url, this.bases, this.tenantId);
  }
}
