import type { SiteSeoSettingsDto, UpdateSiteSeoSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SiteSeoSettingsAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  
  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteSeoSettingsDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/seo-settings',
    },
      { apiName: this.apiName,...config });
  

  
  update = (input: UpdateSiteSeoSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteSeoSettingsDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: '/api/site-admin/seo-settings',
      body: input,
    },
      { apiName: this.apiName,...config });
}
