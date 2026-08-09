import type { CreatePageDto, GetPageListInput, MovePageDto, UpdatePageDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { PageDto } from '../../pages/models';

@Injectable({
  providedIn: 'root',
})
export class PageAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  create = (input: CreatePageDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageDto>({
      method: 'POST',
      url: '/api/site-admin/pages',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/site-admin/pages/${id}`,
    },
    { apiName: this.apiName,...config });
  

  findByName = (name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageDto>({
      method: 'GET',
      url: '/api/site-admin/pages/by-name',
      params: { name },
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageDto>({
      method: 'GET',
      url: `/api/site-admin/pages/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetPageListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<PageDto>>({
      method: 'GET',
      url: '/api/site-admin/pages',
      params: { isActive: input.isActive, filter: input.filter },
    },
    { apiName: this.apiName,...config });
  

  move = (id: string, input: MovePageDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageDto>({
      method: 'PUT',
      url: `/api/site-admin/pages/${id}/move`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdatePageDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageDto>({
      method: 'PUT',
      url: `/api/site-admin/pages/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}