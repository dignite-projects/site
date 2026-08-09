import type { CreateContentTypeDto, UpdateContentTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ContentTypeDto } from '../../content-types/models';

@Injectable({
  providedIn: 'root',
})
export class ContentTypeAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  create = (input: CreateContentTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContentTypeDto>({
      method: 'POST',
      url: '/api/site-admin/content-types',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/site-admin/content-types/${id}`,
    },
    { apiName: this.apiName,...config });
  

  findByName = (pageId: string, name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContentTypeDto>({
      method: 'GET',
      url: `/api/site-admin/content-types/by-page/${pageId}/by-name`,
      params: { name },
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContentTypeDto>({
      method: 'GET',
      url: `/api/site-admin/content-types/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<ContentTypeDto>>({
      method: 'GET',
      url: '/api/site-admin/content-types',
    },
    { apiName: this.apiName,...config });
  

  getListByPage = (pageId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<ContentTypeDto>>({
      method: 'GET',
      url: `/api/site-admin/content-types/by-page/${pageId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateContentTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContentTypeDto>({
      method: 'PUT',
      url: `/api/site-admin/content-types/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}