import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface FieldDto extends FullAuditedEntityDto<string> {
  name?: string;
  displayName?: string;
  description?: string | null;
  fieldTypeName?: string;
  configuration?: Record<string, object>;
  groupName?: string | null;
}

export interface FieldTypeDto {
  name?: string;
  indexable?: boolean;
  composite?: boolean;
}
