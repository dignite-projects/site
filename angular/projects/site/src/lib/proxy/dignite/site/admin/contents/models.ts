import type { ContentStatus } from '../../contents/content-status.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { FlexFieldQueryCondition } from '../../../abp/flex-fields/models';

export interface CreateContentDto {
  contentTypeId: string;
  cultureName: string;
  slug?: string;
  publishTime: string;
  status?: ContentStatus;
  fieldValues?: Record<string, object> | null;
}

export interface GetContentListInput extends PagedAndSortedResultRequestDto {
  pageId?: string | null;
  cultureName?: string | null;
  contentTypeId?: string | null;
  status?: ContentStatus | null;
  publishedBefore?: string | null;
  publishedAfter?: string | null;
  filter?: string | null;
  flexFieldConditions?: FlexFieldQueryCondition[] | null;
}

export interface UpdateContentDto {
  slug?: string;
  publishTime: string;
  status?: ContentStatus;
  fieldValues?: Record<string, object> | null;
  contentTypeId?: string | null;
}
