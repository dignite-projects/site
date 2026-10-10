import { Injectable, inject } from '@angular/core';
import { EnvironmentService } from '@abp/ng.core';
import { absoluteFileUrl } from './site-file-url';

/**
 * Where this admin UI shows Site's files from. A stored address is relative (`/api/site-public/files/...`,
 * see `SITE_FILE_PATH`), and this UI is served from an origin of its own, so the previews
 * (`site-file-preview`: the file picker and modal, a file field, the SEO share image) put the `SiteAdmin`
 * API's host - `environment.apis.SiteAdmin.url`, where the file library's admin API lives - in front of it.
 * The CKEditor field does the same inside the editor (`siteCKEditorConfigContributor`). Nothing that is
 * saved carries that host.
 */
@Injectable({ providedIn: 'root' })
export class SiteFileUrlService {
  private readonly environment = inject(EnvironmentService);

  /** The `SiteAdmin` API's base URL, with no trailing slash; empty when the UI shares its API's origin. */
  get apiBase(): string {
    // getApiUrl itself, guarded: it dereferences `apis` unchecked, and an environment without them (a test
    // bed, say) means there is no other origin to show files from.
    const apis = this.environment.getEnvironment()?.apis;
    return apis ? (this.environment.getApiUrl('SiteAdmin') ?? '').replace(/\/+$/, '') : '';
  }

  /** A stored file address as this UI loads it - see `absoluteFileUrl`. */
  toDisplay(url: string | null | undefined): string {
    return absoluteFileUrl(url, this.apiBase);
  }
}
