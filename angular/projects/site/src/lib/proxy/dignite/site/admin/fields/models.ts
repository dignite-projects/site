
export interface CreateFieldDto {
  name: string;
  displayName: string;
  fieldTypeName: string;
  description?: string | null;
  configuration?: Record<string, object> | null;
  groupName?: string | null;
}

export interface GetFieldListInput {
  filter?: string | null;
}

export interface RenameFieldDto {
  newName: string;
}

export interface UpdateFieldDto {
  displayName: string;
  fieldTypeName: string;
  description?: string | null;
  configuration?: Record<string, object> | null;
  groupName?: string | null;
}
