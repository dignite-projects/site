import type { CreateContentDto, GetContentListInput, UpdateContentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ContentDto } from '../../contents/models';

@Injectable({
  providedIn: 'root',
})
export class ContentAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  create = (input: CreateContentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContentDto>({
      method: 'POST',
      url: '/api/site-admin/contents',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/site-admin/contents/${id}`,
    },
    { apiName: this.apiName,...config });
  

  findBySlug = (pageId: string, cultureName: string, slug: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContentDto>({
      method: 'GET',
      url: '/api/site-admin/contents/by-slug',
      params: { pageId, cultureName, slug },
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContentDto>({
      method: 'GET',
      url: `/api/site-admin/contents/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetContentListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ContentDto>>({
      method: 'GET',
      url: '/api/site-admin/contents',
      params: { pageId: input.pageId, cultureName: input.cultureName, contentTypeId: input.contentTypeId, status: input.status, publishedBefore: input.publishedBefore, publishedAfter: input.publishedAfter, filter: input.filter, flexFieldConditions: input.flexFieldConditions, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateContentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContentDto>({
      method: 'PUT',
      url: `/api/site-admin/contents/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}