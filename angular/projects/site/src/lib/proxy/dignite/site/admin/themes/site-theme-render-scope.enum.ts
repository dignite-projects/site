import { mapEnumToOptions } from '@abp/ng.core';

export enum SiteThemeRenderScope {
  All = 0,
  None = 1,
  Affected = 2,
}

export const siteThemeRenderScopeOptions = mapEnumToOptions(SiteThemeRenderScope);
