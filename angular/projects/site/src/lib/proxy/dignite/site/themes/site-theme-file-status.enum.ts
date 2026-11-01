import { mapEnumToOptions } from '@abp/ng.core';

export enum SiteThemeFileStatus {
  Unchanged = 0,
  Modified = 1,
  Added = 2,
  Deleted = 3,
}

export const siteThemeFileStatusOptions = mapEnumToOptions(SiteThemeFileStatus);
