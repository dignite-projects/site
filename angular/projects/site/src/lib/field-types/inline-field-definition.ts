/**
 * One field defined inline as part of a composite field type's own configuration - a `Matrix` block
 * type's sub-field, or a `Table` column. Mirrors `InlineFieldDefinition`
 * (`src/Dignite.FlexFields.Site/Dignite/FlexFields/Site/InlineFieldDefinition.cs`), shared between the
 * two rather than each declaring its own copy.
 *
 * Camel-cased throughout, matching how the server's `JsonSerializerDefaults.Web` always writes it back
 * out - nothing here is ever authored in a different casing first, so there is no dual-casing round trip
 * to normalize (unlike `Select.Options`/`SelectListItem`).
 */
export interface InlineFieldDefinition {
  name: string;
  displayName: string;
  description?: string;
  fieldTypeName: string;
  required: boolean;
  configuration: Record<string, unknown>;
}

function normalizeInlineFieldDefinition(item: unknown): InlineFieldDefinition {
  const source = (item ?? {}) as Partial<InlineFieldDefinition>;
  return {
    name: source.name ?? '',
    displayName: source.displayName ?? '',
    description: source.description,
    fieldTypeName: source.fieldTypeName ?? '',
    required: source.required ?? false,
    configuration: (source.configuration as Record<string, unknown>) ?? {},
  };
}

/** Reads a stored `Matrix.BlockTypes[].fields` or `Table.Columns` configuration value, defensively. */
export function normalizeInlineFieldDefinitions(source: unknown): InlineFieldDefinition[] {
  return Array.isArray(source) ? source.map(normalizeInlineFieldDefinition) : [];
}
