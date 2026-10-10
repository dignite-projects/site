/**
 * Where the share image is picked from and the size it is shared at (GitHub issue #72).
 *
 * The container mirrors `SiteFileContainerNames.Images` on the server - fixed, not a field setting: the
 * share image is always a picture, and `site-images` is the container whose policy is pictures only.
 * The size mirrors `OpenGraphConsts`: the server emits a file-library `og:image` cropped to exactly
 * this, so previewing it at the same crop shows the editor what a shared link will look like.
 */
export const OG_IMAGE_CONTAINER_NAME = 'site-images';

export const OG_IMAGE_WIDTH = 1200;

export const OG_IMAGE_HEIGHT = 630;
