import type { SiteTemplateKind } from '../../templates/site-template-kind.enum';

export interface CreateSiteTemplateDto {
  name: string;
  body: string;
  comment?: string | null;
  publish?: boolean;
}

export interface GetSiteTemplateListInput {
  filter?: string | null;
}

export interface SiteTemplatePageUsageDto {
  pageId?: string;
  name?: string;
  displayName?: string;
  route?: string;
  template?: string;
  isHomePage?: boolean;
  hasSlug?: boolean;
  path?: string;
  sampleContent?: SiteTemplateSampleContentDto | null;
}

export interface SiteTemplateReferenceDto {
  templateId?: string;
  templateName?: string;
  kind?: SiteTemplateKind;
  versionNumber?: number;
  isPublished?: boolean;
  functionName?: string;
}

export interface SiteTemplateSampleContentDto {
  contentId?: string;
  slug?: string;
  cultureName?: string;
  path?: string;
}

export interface SiteTemplateUnmatchedPageDto {
  pageId?: string;
  name?: string;
  displayName?: string;
  template?: string;
  templateId?: string | null;
  hasDraft?: boolean;
}

export interface SiteTemplateUsageDto {
  templateId?: string;
  templateName?: string;
  kind?: SiteTemplateKind;
  pages?: SiteTemplatePageUsageDto[];
  referencedBy?: SiteTemplateReferenceDto[];
}

export interface UpdateSiteTemplateDto {
  body: string;
  comment?: string | null;
  publish?: boolean;
}
