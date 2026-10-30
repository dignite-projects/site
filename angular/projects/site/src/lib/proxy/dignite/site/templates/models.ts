import type { CreationAuditedEntityDto, FullAuditedEntityDto } from '@abp/ng.core';

export interface SiteTemplateDto extends FullAuditedEntityDto<string> {
  name: string;
  publishedVersionNumber?: number;
  latestVersionNumber: number;
}

export interface SiteTemplatePreviewDto {
  html: string;
  path: string;
  pageName: string;
  cultureName: string;
  pageTemplate: string;
  versionNumber: number;
}

export interface SiteTemplateVersionDto extends CreationAuditedEntityDto<string> {
  templateId: string;
  number: number;
  body: string;
  comment?: string;
}
