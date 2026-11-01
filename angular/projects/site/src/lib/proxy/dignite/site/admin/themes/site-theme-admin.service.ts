import type { PublishSiteThemeDto, RenameSiteThemeFileDto, SaveSiteThemeFileDto, SiteReleaseDetailDto, SiteReleaseDto, SiteReleaseFileContentDto, SiteThemeCheckDto, SiteThemeDto, SiteThemeFileDto, SiteThemeFileSummaryDto, SiteThemeFileUsagesDto, SiteThemeRenameResultDto, UpdateSiteThemeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { SiteThemePreviewDto, SiteThemePreviewLinkDto } from '../../themes/models';

@Injectable({
  providedIn: 'root',
})
export class SiteThemeAdminService {
  private restService = inject(RestService);
  apiName = 'SiteAdmin';


  check = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeCheckDto>({
      method: 'GET',
      url: '/api/site-admin/theme/check',
    },
    { apiName: this.apiName,...config });


  deleteFile = (path: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/site-admin/theme/files/by-path',
      params: { path },
    },
    { apiName: this.apiName,...config });


  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeDto>({
      method: 'GET',
      url: '/api/site-admin/theme',
    },
    { apiName: this.apiName,...config });


  getFile = (path: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeFileDto>({
      method: 'GET',
      url: '/api/site-admin/theme/files/by-path',
      params: { path },
    },
    { apiName: this.apiName,...config });


  getFileUsages = (path: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeFileUsagesDto>({
      method: 'GET',
      url: '/api/site-admin/theme/files/usages',
      params: { path },
    },
    { apiName: this.apiName,...config });


  getFiles = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<SiteThemeFileSummaryDto>>({
      method: 'GET',
      url: '/api/site-admin/theme/files',
    },
    { apiName: this.apiName,...config });


  getPreviewLink = (path?: string, releaseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemePreviewLinkDto>({
      method: 'GET',
      url: '/api/site-admin/theme/preview-link',
      params: { path, releaseId },
    },
    { apiName: this.apiName,...config });


  getRelease = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteReleaseDetailDto>({
      method: 'GET',
      url: `/api/site-admin/theme/releases/${id}`,
    },
    { apiName: this.apiName,...config });


  getReleaseFile = (id: string, path: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteReleaseFileContentDto>({
      method: 'GET',
      url: `/api/site-admin/theme/releases/${id}/files/by-path`,
      params: { path },
    },
    { apiName: this.apiName,...config });


  getReleases = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<SiteReleaseDto>>({
      method: 'GET',
      url: '/api/site-admin/theme/releases',
    },
    { apiName: this.apiName,...config });


  preview = (path: string, releaseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemePreviewDto>({
      method: 'GET',
      url: '/api/site-admin/theme/preview',
      params: { path, releaseId },
    },
    { apiName: this.apiName,...config });


  publish = (input: PublishSiteThemeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteReleaseDto>({
      method: 'POST',
      url: '/api/site-admin/theme/publish',
      body: input,
    },
    { apiName: this.apiName,...config });


  renameFile = (input: RenameSiteThemeFileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeRenameResultDto>({
      method: 'POST',
      url: '/api/site-admin/theme/files/rename',
      body: input,
    },
    { apiName: this.apiName,...config });


  restoreRelease = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/site-admin/theme/releases/${id}/restore`,
    },
    { apiName: this.apiName,...config });


  saveFile = (input: SaveSiteThemeFileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeFileDto>({
      method: 'PUT',
      url: '/api/site-admin/theme/files',
      body: input,
    },
    { apiName: this.apiName,...config });


  setLiveRelease = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteReleaseDto>({
      method: 'POST',
      url: `/api/site-admin/theme/releases/${id}/set-live`,
    },
    { apiName: this.apiName,...config });


  update = (input: UpdateSiteThemeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SiteThemeDto>({
      method: 'PUT',
      url: '/api/site-admin/theme',
      body: input,
    },
    { apiName: this.apiName,...config });
}
