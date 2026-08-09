
export interface CreatePageDto {
  name: string;
  displayName: string;
  route: string;
  template?: string | null;
  isHomePage?: boolean;
  order?: number;
  parentId?: string | null;
  isActive?: boolean;
}

export interface GetPageListInput {
  isActive?: boolean | null;
  filter?: string | null;
}

export interface MovePageDto {
  parentId?: string | null;
  order?: number;
}

export interface UpdatePageDto {
  name: string;
  displayName: string;
  route: string;
  template?: string | null;
  isHomePage?: boolean;
  order?: number;
  parentId?: string | null;
  isActive?: boolean;
}
