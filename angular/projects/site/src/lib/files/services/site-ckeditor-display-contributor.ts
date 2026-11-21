import { inject } from '@angular/core';
import type { CKEditorDisplayContributor } from '@dignite/ng.flex-fields-ckeditor';
import { BLOB_MANAGEMENT_PUBLIC_FILE_PATH, SITE_FILE_PATH } from './site-file-url';
import { SiteFileUrlService } from './site-file-url.service';

/**
 * Lets the read-only rich-text view (`ff-ckeditor-view`: a content list cell, say) show Site's images on the
 * host this admin UI reaches them at. The stored HTML holds relative addresses (`<img src="/blobs/...">`, or an
 * earlier `/api/site-public/files/...`), which do not resolve when this admin UI is served from another origin. The editor
 * itself is handled by `siteCKEditorConfigContributor`; this is its counterpart for the view, which has no
 * editor to convert in.
 *
 * Only `src` is rewritten, in the double- and single-quoted spellings; links (`<a href>`) are left alone, as
 * in the editor. The result is for display only: the view never hands HTML back, so nothing saved carries the
 * host. When a stored address loads as it is (`loadsStoredAddresses`), the HTML comes back untouched.
 *
 * Registered by `provideSite()` under `CKEDITOR_DISPLAY_CONTRIBUTORS` (with `useFactory`, so it can inject);
 * the pattern is the one in `@dignite/ng.flex-fields-ckeditor`'s README, "Customizing the read-only view".
 */
export function siteCKEditorDisplayContributor(): CKEditorDisplayContributor {
  const fileUrls = inject(SiteFileUrlService);
  const paths = `(?:${escapeRegExp(BLOB_MANAGEMENT_PUBLIC_FILE_PATH)}|${escapeRegExp(SITE_FILE_PATH)})`;
  const relativeSrc = new RegExp(String.raw`(\ssrc\s*=\s*)(["'])(${paths}[^"']*)\2`, 'gi');

  return html => {
    if (fileUrls.loadsStoredAddresses) {
      return html;
    }

    return html.replace(
      relativeSrc,
      (_match, prefix: string, quote: string, address: string) => prefix + quote + fileUrls.toDisplay(address) + quote,
    );
  };
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
