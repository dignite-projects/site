
export interface CreateSiteTemplateDto {
  name: string;
  body: string;
  comment?: string | null;
  publish?: boolean;
}

export interface GetSiteTemplateListInput {
  filter?: string | null;
}

export interface UpdateSiteTemplateDto {
  body: string;
  comment?: string | null;
  publish?: boolean;
}
