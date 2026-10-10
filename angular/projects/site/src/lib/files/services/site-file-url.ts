/**
 * The path every Site file is served under - the server's `SiteFileUrl.RoutePrefix`. The admin API hands
 * out absolute addresses on it (`FileDescriptorDto.url`), which is what a file field stores.
 */
export const SITE_FILE_PATH = '/api/site-public/files/';

/** Whether `url` addresses a file Site serves (and so can be resized), as opposed to an external image. */
export function isSiteFileUrl(url: string | null | undefined): boolean {
  return !!url && url.toLowerCase().includes(SITE_FILE_PATH);
}

/**
 * `url` asked for at `width` x `height` (`?Width=&Height=`, the server crops to fill when both are given),
 * keeping every other query parameter - `__tenant` in particular. Anything that is not a Site file address
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
