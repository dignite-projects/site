import type { CreateSiteTemplateDto, GetSiteTemplateListInput, UpdateSiteTemplateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { SiteTemplateDto, SiteTemplatePreviewDto, SiteTemplateVersionDto } from '../../templates/models';

@Injectable({
  providedIn: 'root',
})
export class SiteTemplateAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  
  create = (input: CreateSiteTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteTemplateDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/templates',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/site-admin/templates/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  findByName = (name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteTemplateDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/templates/by-name',
      params: { name },
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteTemplateDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/site-admin/templates/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetSiteTemplateListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<SiteTemplateDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/templates',
      params: { filter: input.filter },
    },
      { apiName: this.apiName,...config });
  

  
  getVersion = (id: string, versionNumber: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteTemplateVersionDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/site-admin/templates/${id}/versions/${versionNumber}`,
    },
      { apiName: this.apiName,...config });
  

  
  getVersions = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<SiteTemplateVersionDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/site-admin/templates/${id}/versions`,
    },
      { apiName: this.apiName,...config });
  

  
  preview = (id: string, versionNumber: number, path: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteTemplatePreviewDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/site-admin/templates/${id}/versions/${versionNumber}/preview`,
      params: { path },
    },
      { apiName: this.apiName,...config });
  

  
  publish = (id: string, versionNumber: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteTemplateDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/site-admin/templates/${id}/publish/${versionNumber}`,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: UpdateSiteTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteTemplateDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/site-admin/templates/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
