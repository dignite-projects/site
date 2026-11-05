import type { CreateSiteThemeExportLinkInput, CreateSiteThemeImportLinkInput, ImportSiteThemeInput, SiteThemeImportResultDto, SiteThemeTransferLinkDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SiteThemeTransferAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  
  createExportLink = (input: CreateSiteThemeExportLinkInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeTransferLinkDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/theme/export-links',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createImportLink = (input: CreateSiteThemeImportLinkInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeTransferLinkDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/theme/import-links',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  downloadByTicket = (ticket: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      headers: { Accept: 'application/octet-stream' },
      url: `/api/site-admin/theme/download/${ticket}`,
    },
      { apiName: this.apiName,...config });
  

  
  export = (releaseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      headers: { Accept: 'application/octet-stream' },
      url: '/api/site-admin/theme/export',
      params: { releaseId },
    },
      { apiName: this.apiName,...config });
  

  
  import = (input: FormData, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeImportResultDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/theme/import',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  uploadByTicket = (ticket: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeImportResultDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/site-admin/theme/upload/${ticket}`,
    },
      { apiName: this.apiName,...config });
}
