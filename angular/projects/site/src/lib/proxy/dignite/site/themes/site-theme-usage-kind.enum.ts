import { mapEnumToOptions } from '@abp/ng.core';

export enum SiteThemeUsageKind {
  Page = 0,
  Include = 1,
  AssetUrl = 2,
  Layout = 3,
  DefaultLayout = 4,
}

export const siteThemeUsageKindOptions = mapEnumToOptions(SiteThemeUsageKind);
