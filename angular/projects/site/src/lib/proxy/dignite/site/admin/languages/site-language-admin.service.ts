import type { SiteLanguagesDto, UpdateSiteLanguagesDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SiteLanguageAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';


  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteLanguagesDto>({
      method: 'GET',
      url: '/api/site-admin/languages',
    },
    { apiName: this.apiName,...config });


  update = (input: UpdateSiteLanguagesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteLanguagesDto>({
      method: 'PUT',
      url: '/api/site-admin/languages',
      body: input,
    },
    { apiName: this.apiName,...config });
}
