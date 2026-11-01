import { mapEnumToOptions } from '@abp/ng.core';

export enum SiteThemeIssueSeverity {
  Error = 0,
  Warning = 1,
}

export const siteThemeIssueSeverityOptions = mapEnumToOptions(SiteThemeIssueSeverity);
