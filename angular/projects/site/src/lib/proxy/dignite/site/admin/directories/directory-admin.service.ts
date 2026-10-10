import type { CreateDirectoryInput, DirectoryDescriptorDto, DirectoryDescriptorInfoDto, GetDirectoriesInput, MoveDirectoryInput, UpdateDirectoryInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DirectoryAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  create = (input: CreateDirectoryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DirectoryDescriptorDto>({
      method: 'POST',
      url: '/api/site-admin/directories',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/site-admin/directories/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DirectoryDescriptorDto>({
      method: 'GET',
      url: `/api/site-admin/directories/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetDirectoriesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<DirectoryDescriptorInfoDto>>({
      method: 'GET',
      url: '/api/site-admin/directories',
      params: { containerName: input.containerName },
    },
    { apiName: this.apiName,...config });
  

  move = (id: string, input: MoveDirectoryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DirectoryDescriptorDto>({
      method: 'PUT',
      url: `/api/site-admin/directories/${id}/move`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateDirectoryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DirectoryDescriptorDto>({
      method: 'PUT',
      url: `/api/site-admin/directories/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}