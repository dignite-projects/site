
export interface CreatePageDto {
  name: string;
  displayName: string;
  route: string;
  template?: string | null;
  contentTemplate?: string | null;
  parentId?: string | null;
  isActive?: boolean;
}

export interface GetPageListInput {
  isActive?: boolean | null;
  filter?: string | null;
}

export interface UpdatePageDto {
  name: string;
  displayName: string;
  route: string;
  template?: string | null;
  contentTemplate?: string | null;
  parentId?: string | null;
  isActive?: boolean;
}
