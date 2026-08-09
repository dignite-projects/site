import type { ContentTypeFieldDto } from '../../content-types/models';

export interface CreateContentTypeDto {
  pageId: string;
  name: string;
  displayName: string;
  description?: string | null;
  fields?: ContentTypeFieldDto[] | null;
}

export interface UpdateContentTypeDto {
  name: string;
  displayName: string;
  description?: string | null;
  fields?: ContentTypeFieldDto[] | null;
}
