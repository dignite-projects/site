import type { CreateSiteFileUploadTicketInput, GetSiteFilesInput, SiteFileContainerDto, SiteFileDto, SiteFileUploadTicketDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class FileAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  
  confirm = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteFileDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/site-admin/files/${id}/confirm`,
    },
      { apiName: this.apiName,...config });
  

  
  createUploadTicket = (input: CreateSiteFileUploadTicketInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteFileUploadTicketDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/files/upload-tickets',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/site-admin/files/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteFileDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/site-admin/files/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getContainers = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<SiteFileContainerDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/files/containers',
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetSiteFilesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SiteFileDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/files',
      params: { containerName: input.containerName, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
}
