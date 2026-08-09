import type { FullAuditedEntityDto } from '@abp/ng.core';
import type { ContentStatus } from './content-status.enum';

export interface ContentDto extends FullAuditedEntityDto<string> {
  pageId?: string;
  contentTypeId?: string;
  cultureName?: string;
  slug?: string;
  publishTime?: string;
  status?: ContentStatus;
  fieldValues?: Record<string, object>;
}
