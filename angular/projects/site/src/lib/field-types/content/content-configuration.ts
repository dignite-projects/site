/**
 * Configuration of a `Content` field, shaped for `FormBuilder.group()`. Mirrors `ContentConfiguration`
 * (`src/Dignite.FlexFields.Site/Dignite/FlexFields/Site/Content/ContentConfiguration.cs`).
 *
 * The property names are the **stored** configuration keys, not a naming choice.
 */
export class ContentConfiguration {
  /** Restricts the picker to Content of this content type. `null` = unrestricted. */
  'Content.ContentTypeId': unknown = [null];

  'Content.Multiple': unknown = [false];

  'Content.Placeholder': unknown = [''];
}
