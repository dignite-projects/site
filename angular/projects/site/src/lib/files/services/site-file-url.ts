/**
 * The path every Site file is served under - the server's `SiteFileUrl.RoutePrefix`. The admin API hands
 * out addresses relative to it (`FileDescriptorDto.url`, `/api/site-public/files/...`), and that
 * relative address is what a field stores: no host is written into content. The public site resolves it
 * against its own origin; this admin UI, served from another origin, shows it from its `SiteAdmin` API
 * host (`SiteFileUrlService`).
 */
export const SITE_FILE_PATH = '/api/site-public/files/';

/** Whether `url` addresses a file Site serves (and so can be resized), as opposed to an external image. */
export function isSiteFileUrl(url: string | null | undefined): boolean {
  return !!url && url.toLowerCase().includes(SITE_FILE_PATH);
}

/**
 * `url` asked for at `width` x `height` (`?Width=&Height=`, the server crops to fill when both are given),
 * keeping every other query parameter (`__tenant`, on an address an earlier version issued). Anything that is not a Site file address
 * (a local `blob:` URL, an external image) comes back unchanged, as does a call without a size. Mirrors the
 * server's `SiteFileUrl.Sized`.
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

  const size = [width ? `Width=${width}` : '', height ? `Height=${height}` : ''].filter(Boolean);
  return `${base}?${[...kept, ...size].join('&')}`;
}

/**
 * `url` as this admin UI loads it: a relative Site file address, on `apiBase` and naming the tenant
 * (`?__tenant=`) when `tenantId` is given. A stored address names no tenant, and an `<img>` request carries
 * none of the headers the UI's API calls do, so the host could not tell which tenant's file is meant. An
 * address that already has a `__tenant` (one an earlier version issued) keeps it. Anything else - an absolute
 * address, a local `blob:`/`data:` URL, an external image - comes back unchanged, as does every relative
 * address when `apiBase` and `tenantId` are both empty. Mirrors the server's `SiteFileUrl.Absolutize`.
 */
export function absoluteFileUrl(url: string | null | undefined, apiBase: string, tenantId?: string | null): string {
  const value = url ?? '';
  if (!value.toLowerCase().startsWith(SITE_FILE_PATH)) {
    return value;
  }

  return trimBase(apiBase) + withTenant(value, tenantId);
}

/**
 * The inverse of `absoluteFileUrl`: an address on `apiBase`, or a relative one, as it is stored - relative,
 * without the `__tenant` that `absoluteFileUrl` added for `tenantId`. Another tenant's `__tenant`, an empty
 * one and every address that is not a Site file come back unchanged.
 */
export function relativeFileUrl(url: string | null | undefined, apiBase: string, tenantId?: string | null): string {
  const value = url ?? '';
  const base = trimBase(apiBase);
  if (base && value.toLowerCase().startsWith(base.toLowerCase() + SITE_FILE_PATH)) {
    return withoutTenant(value.substring(base.length), tenantId);
  }

  return value.toLowerCase().startsWith(SITE_FILE_PATH) ? withoutTenant(value, tenantId) : value;
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
