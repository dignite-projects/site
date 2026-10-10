import type { CreateFileInput, FileContainerConfigurationDto, FileDescriptorDto, GetFilesInput, UpdateFileInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class FileAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  create = (input: CreateFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FileDescriptorDto>({
      method: 'POST',
      url: '/api/site-admin/files',
      params: { containerName: input.containerName, directoryId: input.directoryId },
      body: input.file,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/site-admin/files/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FileDescriptorDto>({
      method: 'GET',
      url: `/api/site-admin/files/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getContainerConfiguration = (containerName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FileContainerConfigurationDto>({
      method: 'GET',
      url: `/api/site-admin/files/containers/${containerName}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetFilesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<FileDescriptorDto>>({
      method: 'GET',
      url: '/api/site-admin/files',
      params: { containerName: input.containerName, directoryId: input.directoryId, creatorId: input.creatorId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FileDescriptorDto>({
      method: 'PUT',
      url: `/api/site-admin/files/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}