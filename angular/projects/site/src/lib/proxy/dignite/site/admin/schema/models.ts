
export interface SiteSchemaContentTypeDto {
  name?: string;
  displayName?: string;
  description?: string | null;
  fields?: SiteSchemaFieldDto[];
}

export interface SiteSchemaDto {
  enabledLanguages?: string[];
  defaultLanguage?: string;
  primaryDomain?: string;
  pages?: SiteSchemaPageDto[];
}

export interface SiteSchemaFieldDto {
  name?: string;
  displayName?: string;
  description?: string | null;
  fieldTypeName?: string;
  configuration?: Record<string, object>;
  required?: boolean;
  searchable?: boolean;
  order?: number;
  showInList?: boolean;
}

export interface SiteSchemaPageDto {
  name?: string;
  displayName?: string;
  route?: string;
  isHomePage?: boolean;
  isActive?: boolean;
  parent?: string | null;
  contentTypes?: SiteSchemaContentTypeDto[];
}
