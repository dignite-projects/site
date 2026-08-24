import type { InlineFieldDefinition } from '../inline-field-definition';
import { normalizeInlineFieldDefinitions } from '../inline-field-definition';

/** One block type a `Matrix` field's configuration declares. Mirrors `MatrixBlockType`. */
export interface MatrixBlockType {
  name: string;
  displayName: string;
  fields: InlineFieldDefinition[];
}

/** One block instance - what a `Matrix` field's value is a list of. Mirrors `MatrixBlockValue`. */
export interface MatrixBlockValue {
  blockTypeName: string;
  values: Record<string, unknown>;
}

/** Reads a stored `Matrix.BlockTypes` configuration value, defensively. */
export function normalizeMatrixBlockTypes(source: unknown): MatrixBlockType[] {
  if (!Array.isArray(source)) {
    return [];
  }

  return source.map(item => ({
    name: (item?.name as string) ?? '',
    displayName: (item?.displayName as string) ?? '',
    fields: normalizeInlineFieldDefinitions(item?.fields),
  }));
}

/** Reads a stored Matrix field's value - a list of block instances - defensively. */
export function normalizeMatrixBlockValues(source: unknown): MatrixBlockValue[] {
  if (!Array.isArray(source)) {
    return [];
  }

  return source.map(item => {
    const value = (item ?? {}) as Partial<MatrixBlockValue>;
    return {
      blockTypeName: value.blockTypeName ?? '',
      values: (value.values as Record<string, unknown>) ?? {},
    };
  });
}
