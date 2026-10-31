import type { SiteTemplateKind } from './site-template-kind.enum';
import type { CreationAuditedEntityDto, FullAuditedEntityDto } from '@abp/ng.core';

export interface SiteTemplateDto extends FullAuditedEntityDto<string> {
  name?: string;
  kind?: SiteTemplateKind;
  publishedVersionNumber?: number | null;
  latestVersionNumber?: number;
  usageCount?: number | null;
}

export interface SiteTemplatePreviewDto {
  html?: string;
  path?: string;
  pageName?: string;
  cultureName?: string;
  pageTemplate?: string;
  versionNumber?: number;
}

export interface SiteTemplatePreviewLinkDto {
  url?: string;
  path?: string;
  templateName?: string;
  versionNumber?: number;
  expiresAt?: string;
}

export interface SiteTemplateVersionDto extends CreationAuditedEntityDto<string> {
  templateId?: string;
  number?: number;
  body?: string;
  comment?: string | null;
}
