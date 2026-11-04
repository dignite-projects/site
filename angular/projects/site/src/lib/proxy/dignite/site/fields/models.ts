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
  valueShape?: FieldValuePropertyDto[] | null;
  configurationKeys?: FieldConfigurationKeyDto[] | null;
}

export interface FieldConfigurationKeyDto {
  name?: string;
  type?: string;
  description?: string | null;
  defaultValue?: object | null;
  required?: boolean;
}

export interface FieldValuePropertyDto {
  name?: string;
  type?: string;
  description?: string | null;
}
