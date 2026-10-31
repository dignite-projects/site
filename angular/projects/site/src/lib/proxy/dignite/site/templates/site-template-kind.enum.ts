import { mapEnumToOptions } from '@abp/ng.core';

export enum SiteTemplateKind {
  Page = 0,
  Layout = 1,
  Partial = 2,
  Asset = 3,
}

export const siteTemplateKindOptions = mapEnumToOptions(SiteTemplateKind);
