/**
 * Where the share image is uploaded to and the size it is shared at (GitHub issue #72).
 *
 * The container mirrors `SiteFileContainerNames.Images` on the server - fixed, not a field setting: the
 * share image is always a picture, and `images` is the container whose preset takes pictures only.
 * The size mirrors `OpenGraphConsts`: the server emits a file-library `og:image` cropped to exactly
 * this, so previewing it at the same crop shows the editor what a shared link will look like.
 */
export const OG_IMAGE_CONTAINER_NAME = 'images';

export const OG_IMAGE_WIDTH = 1200;

export const OG_IMAGE_HEIGHT = 630;

/**
 * A stored share image address as the value of a `site-file-input`: one file object with the address (its id, which
 * the input tracks the file by, is the address too), or none for an empty one. What the input hands back is the
 * stored file, whose `url` is what the field stores.
 */
export function fileOfUrl(url: string | null | undefined): { id: string; name: string; url: string }[] {
  const value = (url ?? '').trim();
  if (!value) {
    return [];
  }

  const path = value.split('?')[0];
  return [{ id: value, name: decodeURIComponent(path.substring(path.lastIndexOf('/') + 1)) || value, url: value }];
}
