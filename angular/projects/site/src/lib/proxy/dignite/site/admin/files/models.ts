import type { IRemoteStreamContent } from '../../../../volo/abp/content/models';
import type { CreationAuditedEntityDto, ExtensibleAuditedEntityDto, ExtensibleEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CreateDirectoryInput {
  containerName: string;
  name: string;
  parentId?: string | null;
}

export interface CreateFileInput {
  containerName: string;
  directoryId?: string | null;
  file: IRemoteStreamContent;
}

export interface DirectoryDescriptorDto extends ExtensibleAuditedEntityDto<string> {
  containerName?: string;
  name?: string;
  parentId?: string | null;
  order?: number;
}

export interface DirectoryDescriptorInfoDto extends ExtensibleEntityDto<string> {
  containerName?: string;
  name?: string;
  parentId?: string | null;
  order?: number;
  children?: DirectoryDescriptorInfoDto[];
}

export interface FileContainerConfigurationDto {
  maxBlobSize?: number;
  allowedFileTypeNames?: string[];
  createDirectoryPermissionName?: string | null;
  createFilePermissionName?: string | null;
  updateFilePermissionName?: string | null;
  deleteFilePermissionName?: string | null;
  getFilePermissionName?: string | null;
}

export interface FileDescriptorDto extends CreationAuditedEntityDto<string> {
  containerName?: string;
  blobName?: string;
  directoryId?: string | null;
  size?: number;
  name?: string;
  mimeType?: string;
  url?: string | null;
  tenantId?: string | null;
}

export interface GetDirectoriesInput {
  containerName: string;
}

export interface GetFilesInput extends PagedAndSortedResultRequestDto {
  containerName: string;
  directoryId?: string | null;
  creatorId?: string | null;
  filter?: string | null;
}

export interface MoveDirectoryInput {
  parentId?: string | null;
  order?: number;
}

export interface UpdateDirectoryInput {
  name: string;
}

export interface UpdateFileInput {
  name?: string | null;
  directoryId?: string | null;
  directoryIdSpecified?: boolean;
}
