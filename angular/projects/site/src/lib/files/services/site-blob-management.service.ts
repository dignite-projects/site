import { Rest, RestService } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import { BLOB_MANAGEMENT_API_NAME } from './site-file-upload.service';

/**
 * What the signed-in user may do with a container's directories, as Blob Management answers it (`access` of a
 * container). `view` is the management permission `BlobManagement.Containers` or the grant
 * `BlobManagement.Container.View` on the container; the other three are grants on the container alone -
 * `BlobManagement.Container.CreateDirectories`, `.UpdateDirectories` and `.DeleteDirectories` - which not even the
 * management permission stands in for.
 */
export interface SiteBlobAccess {
  view: boolean;
  createDirectories: boolean;
  updateDirectories: boolean;
  deleteDirectories: boolean;
}

/** One container as `GET api/blob-management/containers/{name}` describes it - to any signed-in user. */
export interface SiteBlobContainer {
  name?: string | null;
  allowDirectories: boolean;
  maxFileSize: number;
  allowedFileExtensions?: string[] | null;
  publicSizes?: number[] | null;
  imageMaxWidth?: number | null;
  imageMaxHeight?: number | null;
  access: SiteBlobAccess;
}

export interface SiteBlobDirectory {
  id: string;
  containerName?: string | null;
  name: string;
  parentId?: string | null;
  order: number;
  hasChildren: boolean;
}

/**
 * The container endpoint of Blob Management, which the host of Site's administration API installs. Called with the
 * user's own token, as Blob Management's own UI does: a container's limits and what the user may do with its directories
 * are Blob Management's to say. The API is named `BlobManagement`; without an `apis.BlobManagement` entry in the
 * environment ABP uses `apis.default`, which is right when Blob Management is in the host of the `SiteAdmin` API.
 */
@Injectable({ providedIn: 'root' })
export class SiteBlobContainerService {
  private readonly restService = inject(RestService);
  readonly apiName = BLOB_MANAGEMENT_API_NAME;

  get = (name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteBlobContainer>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: `/api/blob-management/containers/${encodeURIComponent(name)}`,
      },
      { apiName: this.apiName, ...config },
    );
}

/**
 * The directories of a container, which Blob Management keeps and authorizes by the grants on the container
 * (`SiteBlobAccess`): Site has no rules of its own about them, so the UI calls Blob Management directly.
 */
@Injectable({ providedIn: 'root' })
export class SiteBlobDirectoryService {
  private readonly restService = inject(RestService);
  readonly apiName = BLOB_MANAGEMENT_API_NAME;

  /** The directories one level down; `parentId` none is the top of the container. */
  getList = (containerName: string, parentId?: string | null, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<SiteBlobDirectory>>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/blob-management/directories',
        params: { containerName, parentId: parentId ?? undefined },
      },
      { apiName: this.apiName, ...config },
    );

  create = (containerName: string, name: string, parentId?: string | null, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteBlobDirectory>(
      {
        method: 'POST',
        headers: { Accept: 'application/json' },
        url: '/api/blob-management/directories',
        body: { containerName, name, parentId: parentId ?? null },
      },
      { apiName: this.apiName, ...config },
    );

  rename = (id: string, name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteBlobDirectory>(
      {
        method: 'PUT',
        headers: { Accept: 'application/json' },
        url: `/api/blob-management/directories/${id}`,
        body: { name },
      },
      { apiName: this.apiName, ...config },
    );

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: `/api/blob-management/directories/${id}`,
      },
      { apiName: this.apiName, ...config },
    );
}
