import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface ContentTypeFieldDto {
  fieldId?: string;
  required?: boolean;
  searchable?: boolean;
  showInList?: boolean;
  displayName?: string | null;
  order?: number;
}

export interface ContentTypeDto extends FullAuditedEntityDto<string> {
  pageId?: string;
  name?: string;
  displayName?: string;
  description?: string | null;
  fields?: ContentTypeFieldDto[];
}
