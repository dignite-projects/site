/**
 * Site's file containers - the server's `SiteFileContainerNames`. The file library works in these only;
 * a file field names one of them.
 */
export const SITE_FILE_CONTAINER_NAMES = ['site-images', 'site-files'] as const;

/** Pictures only (raster formats, no SVG) - what a share image or an image field uses. */
export const SITE_IMAGES_CONTAINER_NAME = 'site-images';
