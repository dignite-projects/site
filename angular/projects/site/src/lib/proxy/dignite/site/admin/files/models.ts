import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CreateSiteFileUploadTicketInput {
  containerName: string;
  directoryId?: string | null;
}

export interface GetSiteFilesInput extends PagedAndSortedResultRequestDto {
  containerName: string;
  filter?: string | null;
  directoryId?: string | null;
}

export interface SiteFileContainerDto {
  name?: string | null;
  maxFileSize: number;
  allowedFileExtensions?: string[] | null;
  imageMaxWidth?: number | null;
  imageMaxHeight?: number | null;
}

export interface SiteFileDto {
  id?: string;
  containerName?: string | null;
  name?: string | null;
  mimeType?: string | null;
  size: number;
  url?: string | null;
  isPending: boolean;
  creatorId?: string | null;
  creationTime?: string;
}

export interface SiteFileUploadTicketDto {
  fileId?: string;
  uploadUrl?: string | null;
  expirationTime?: string;
}
