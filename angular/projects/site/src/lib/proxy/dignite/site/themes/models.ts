
export interface SiteThemePreviewDto {
  html?: string;
  path?: string;
  pageName?: string;
  cultureName?: string;
  pageTemplate?: string;
  layout?: string;
}

export interface SiteThemePreviewLinkDto {
  url?: string;
  path?: string;
  releaseId?: string;
  expiresAt?: string;
}
