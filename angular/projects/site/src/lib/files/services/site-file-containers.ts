/**
 * The file containers Site's fields use - the server's `SiteFileContainerNames`, which the host declares as Blob
 * Management containers: `images` takes pictures, `attachments` documents, `theme-assets` the binary files a theme links.
 * A file field names one of them.
 */
export const SITE_FILE_CONTAINER_NAMES = ['images', 'attachments', 'theme-assets'] as const;

/** Pictures only (raster formats, no SVG) - what a share image (`OG_IMAGE_CONTAINER_NAME`) or an image field uses. */
export const SITE_IMAGES_CONTAINER_NAME = 'images';
