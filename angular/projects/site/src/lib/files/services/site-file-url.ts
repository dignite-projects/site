/**
 * The path of Blob Management's public read (`/blobs/{container}/{blob}`). A file's `url` is this address, relative, with
 * `?__tenant=` naming a tenant's file, and fields, settings and CKEditor's HTML store it as it is. The public site
 * resolves it against its own origin, whose gateway routes `/blobs/*` to Blob Management; this admin UI, served from an
 * origin of its own, shows it from the `BlobManagement` API's host (`SiteFileUrlService`). Mirrors the server's
 * `SiteFileUrl`.
 */
export const BLOB_MANAGEMENT_PUBLIC_FILE_PATH = '/blobs/';

/**
 * Where Site served its files before 3.0 (`/api/site-public/files/...`, the server's `SiteFileUrl.RoutePrefix`). Stored
 * data still holds such addresses; that endpoint now redirects to Blob Management's address of the same file, so they
 * keep working, shown from the `SiteAdmin` API's host.
 */
export const SITE_FILE_PATH = '/api/site-public/files/';

/**
 * The widths and heights Site asks Blob Management to resize its pictures to, which the host allows on Site's
 * containers: Blob Management's default list and the `og:image` box. Mirrors the server's `SiteImageSizes.All`.
 */
export const SITE_IMAGE_SIZES: readonly number[] = [64, 128, 256, 384, 512, 630, 768, 1024, 1200, 1536, 2048, 4096];

/** The smallest of `SITE_IMAGE_SIZES` at least `size`; the largest for a size beyond it. */
export function roundUpImageSize(size: number): number {
  return SITE_IMAGE_SIZES.find(allowed => allowed >= size) ?? SITE_IMAGE_SIZES[SITE_IMAGE_SIZES.length - 1];
}

/**
 * Whether `value` - relative, or absolute on any host - is Blob Management's public address of a file. The prefix is
 * recognized at the start of the address's path only: `/blobs/` inside another path is no file of Blob Management's.
 */
function isBlobManagementAddress(value: string): boolean {
  let pathStart = 0;
  if (!value.startsWith('/')) {
    const schemeEnd = value.indexOf('://');
    if (schemeEnd <= 0) {
      return false;
    }

    pathStart = value.indexOf('/', schemeEnd + 3);
    if (pathStart < 0) {
      return false;
    }
  }

  const path = value.substring(pathStart);
  return path.toLowerCase().startsWith(BLOB_MANAGEMENT_PUBLIC_FILE_PATH) && path.length > BLOB_MANAGEMENT_PUBLIC_FILE_PATH.length;
}

/**
 * Whether `url` addresses one of Site's files (and so can be resized): Blob Management's public address, or an address
 * Site served before 3.0 - as opposed to an external image or a local `blob:` URL.
 */
export function isSiteFileUrl(url: string | null | undefined): boolean {
  return !!url && (isBlobManagementAddress(url) || url.toLowerCase().includes(SITE_FILE_PATH));
}

/**
 * `url` asked for at `width` x `height` (`?width=&height=`; Blob Management crops to fill when both are given), keeping
 * every other query parameter (`__tenant`). Blob Management serves only `SITE_IMAGE_SIZES`, so each side given is
 * rounded up to one of them. The server rounds a side given alone the same way and writes two as asked, so as not to
 * change the box's shape; this admin UI shows a thumbnail in a box of its own, so it rounds both. Anything that is not a
 * Site file address comes back unchanged, as does a call without a size.
 */
export function sizedFileUrl(url: string | null | undefined, width?: number, height?: number): string {
  const value = url ?? '';
  if (!isSiteFileUrl(value) || (!width && !height)) {
    return value;
  }

  const queryStart = value.indexOf('?');
  const base = queryStart < 0 ? value : value.substring(0, queryStart);
  const kept =
    queryStart < 0
      ? []
      : value
          .substring(queryStart + 1)
          .split('&')
          .filter(parameter => {
            const name = parameter.split('=')[0].toLowerCase();
            return parameter && name !== 'width' && name !== 'height';
          });

  const size = [
    width ? `width=${roundUpImageSize(width)}` : '',
    height ? `height=${roundUpImageSize(height)}` : '',
  ].filter(Boolean);
  return `${base}?${[...kept, ...size].join('&')}`;
}

/** Where this admin UI reaches Site's files: Blob Management's public read, and Site's API for an earlier address. */
export interface SiteFileBases {
  /** The `BlobManagement` API's base URL: empty when the UI shares its origin. */
  blobManagement: string;
  /** The `SiteAdmin` API's base URL, which also serves the redirect of an earlier address. */
  site: string;
}

/**
 * `url` as this admin UI loads it. Blob Management's relative address goes on `bases.blobManagement`, as it is - it
 * names its tenant already. An earlier Site address goes on `bases.site` and names the signed-in tenant (`?__tenant=`)
 * when `tenantId` is given: it names none, and an `<img>` request carries none of the headers the UI's API calls do, so
 * the redirect could not tell which tenant's file is meant. Anything else - an absolute address, a local `blob:`/`data:`
 * URL, an external image - comes back unchanged.
 */
export function absoluteFileUrl(url: string | null | undefined, bases: SiteFileBases, tenantId?: string | null): string {
  const value = url ?? '';
  const lower = value.toLowerCase();
  if (lower.startsWith(BLOB_MANAGEMENT_PUBLIC_FILE_PATH)) {
    return trimBase(bases.blobManagement) + value;
  }

  if (lower.startsWith(SITE_FILE_PATH)) {
    return trimBase(bases.site) + withTenant(value, tenantId);
  }

  return value;
}

/**
 * The inverse of `absoluteFileUrl`: an address on one of `bases`, or a relative one, as it is stored - relative, and for
 * an earlier Site address without the `__tenant` that `absoluteFileUrl` added for `tenantId`. Every other address comes
 * back unchanged.
 */
export function relativeFileUrl(url: string | null | undefined, bases: SiteFileBases, tenantId?: string | null): string {
  const value = url ?? '';
  const lower = value.toLowerCase();

  const blobManagement = trimBase(bases.blobManagement);
  if (blobManagement && lower.startsWith(blobManagement.toLowerCase() + BLOB_MANAGEMENT_PUBLIC_FILE_PATH)) {
    return value.substring(blobManagement.length);
  }

  const site = trimBase(bases.site);
  if (site && lower.startsWith(site.toLowerCase() + SITE_FILE_PATH)) {
    return withoutTenant(value.substring(site.length), tenantId);
  }

  return lower.startsWith(SITE_FILE_PATH) ? withoutTenant(value, tenantId) : value;
}

function withTenant(url: string, tenantId: string | null | undefined): string {
  if (!tenantId || /[?&]__tenant=/i.test(url)) {
    return url;
  }

  return `${url}${url.includes('?') ? '&' : '?'}__tenant=${encodeURIComponent(tenantId)}`;
}

function withoutTenant(url: string, tenantId: string | null | undefined): string {
  const queryStart = url.indexOf('?');
  if (!tenantId || queryStart < 0) {
    return url;
  }

  const added = `__tenant=${encodeURIComponent(tenantId)}`.toLowerCase();
  const kept = url
    .substring(queryStart + 1)
    .split('&')
    .filter(parameter => parameter.toLowerCase() !== added);
  const base = url.substring(0, queryStart);
  return kept.length ? `${base}?${kept.join('&')}` : base;
}

function trimBase(apiBase: string | null | undefined): string {
  return (apiBase ?? '').trim().replace(/\/+$/, '');
}
