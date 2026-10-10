import { inject } from '@angular/core';
import type { CKEditorDisplayContributor } from '@dignite/ng.flex-fields-ckeditor';
import { SITE_FILE_PATH } from './site-file-url';
import { SiteFileUrlService } from './site-file-url.service';

/**
 * Lets the read-only rich-text view (`ff-ckeditor-view`: a content list cell, say) show Site's images on the
 * `SiteAdmin` API's host. The stored HTML holds relative addresses (`<img src="/api/site-public/files/...">`,
 * see `SITE_FILE_PATH`), which do not resolve when this admin UI is served from another origin. The editor
 * itself is handled by `siteCKEditorConfigContributor`; this is its counterpart for the view, which has no
 * editor to convert in.
 *
 * Only `src` is rewritten, in the double- and single-quoted spellings; links (`<a href>`) are left alone, as
 * in the editor. The result is for display only: the view never hands HTML back, so nothing saved carries the
 * host. When the UI shares its API's origin (`apiBase` empty) the HTML comes back untouched.
 *
 * Registered by `provideSite()` under `CKEDITOR_DISPLAY_CONTRIBUTORS` (with `useFactory`, so it can inject);
 * the pattern is the one in `@dignite/ng.flex-fields-ckeditor`'s README, "Customizing the read-only view".
 */
export function siteCKEditorDisplayContributor(): CKEditorDisplayContributor {
  const fileUrls = inject(SiteFileUrlService);
  const relativeSrc = new RegExp(String.raw`(\ssrc\s*=\s*["'])(${escapeRegExp(SITE_FILE_PATH)})`, 'gi');

  return html => {
    const apiBase = fileUrls.apiBase;
    return apiBase ? html.replace(relativeSrc, (_match, prefix: string, path: string) => prefix + apiBase + path) : html;
  };
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
