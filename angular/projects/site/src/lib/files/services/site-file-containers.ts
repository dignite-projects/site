/**
 * The Drive containers Site's fields use by default - the server's `SiteFileContainerNames`, which the host declares as
 * Drive user drives: `images` takes pictures, `attachments` documents, `theme-assets` the binary files a theme links.
 * A file field names one of them (or another container the host declares).
 */
export const SITE_FILE_CONTAINER_NAMES = ['images', 'attachments', 'theme-assets'] as const;

/** Pictures only (raster formats, no SVG) - what a share image or an image field uses. */
export const SITE_IMAGES_CONTAINER_NAME = 'images';
