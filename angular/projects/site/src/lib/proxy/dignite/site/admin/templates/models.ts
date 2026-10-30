
export interface CreateSiteTemplateDto {
  name: string;
  body: string;
  comment?: string;
  publish: boolean;
}

export interface GetSiteTemplateListInput {
  filter?: string;
}

export interface UpdateSiteTemplateDto {
  body: string;
  comment?: string;
  publish: boolean;
}
