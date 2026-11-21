import type { SiteFileDto } from '../../proxy/dignite/site/admin/files/models';

/**
 * A file as a field stores it, and as `site-file-input` holds it: the file's id (what deletes it), its name, type, size
 * and Site's address of it (`url`, relative - see `SITE_FILE_PATH`), which is what a template reads. The container is
 * kept for a client that wants to know where the file is. Values a content stored before Site's files moved to Blob
 * Management are the same shape, with a `blobName` besides, which nothing reads.
 */
export interface SiteStoredFile {
  id: string;
  containerName?: string | null;
  name: string;
  mimeType?: string | null;
  size?: number;
  url: string;
}

/** The stored form of a file the server confirmed: only what a field keeps. */
export function toStoredFile(file: SiteFileDto): SiteStoredFile {
  return {
    id: file.id ?? '',
    containerName: file.containerName,
    name: file.name ?? '',
    mimeType: file.mimeType,
    size: file.size,
    url: file.url ?? '',
  };
}
