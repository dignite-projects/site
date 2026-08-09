import type { CreateFieldDto, GetFieldListInput, RenameFieldDto, UpdateFieldDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { FieldDto, FieldTypeDto } from '../../fields/models';

@Injectable({
  providedIn: 'root',
})
export class FieldAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';
  

  create = (input: CreateFieldDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FieldDto>({
      method: 'POST',
      url: '/api/site-admin/fields',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/site-admin/fields/${id}`,
    },
    { apiName: this.apiName,...config });
  

  findByName = (name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FieldDto>({
      method: 'GET',
      url: '/api/site-admin/fields/by-name',
      params: { name },
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FieldDto>({
      method: 'GET',
      url: `/api/site-admin/fields/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getFieldTypes = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<FieldTypeDto>>({
      method: 'GET',
      url: '/api/site-admin/fields/field-types',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetFieldListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<FieldDto>>({
      method: 'GET',
      url: '/api/site-admin/fields',
      params: { filter: input.filter },
    },
    { apiName: this.apiName,...config });
  

  rename = (id: string, input: RenameFieldDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FieldDto>({
      method: 'POST',
      url: `/api/site-admin/fields/${id}/rename`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateFieldDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FieldDto>({
      method: 'PUT',
      url: `/api/site-admin/fields/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}