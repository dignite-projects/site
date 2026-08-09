import type { SiteSchemaDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SiteSchemaAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteSchemaDto>({
      method: 'GET',
      url: '/api/site-admin/schema',
    },
    { apiName: this.apiName,...config });
}