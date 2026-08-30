import type { FullAuditedEntityDto } from '@abp/ng.core';
import type { ContentTypeDto } from '../content-types/models';

export interface PageDto extends FullAuditedEntityDto<string> {
  name?: string;
  displayName?: string;
  route?: string;
  template?: string;
  isHomePage?: boolean;
  parentId?: string | null;
  isActive?: boolean;
  contentTypes?: ContentTypeDto[] | null;
}
