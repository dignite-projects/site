import { mapEnumToOptions } from '@abp/ng.core';

export enum ContentStatus {
  Draft = 0,
  Published = 1,
  Archived = 2,
}

export const contentStatusOptions = mapEnumToOptions(ContentStatus);
