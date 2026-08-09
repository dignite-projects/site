/**
 * Configuration of a `Seo` field, shaped for `FormBuilder.group()`. Mirrors `SeoConfiguration`
 * (`src/Dignite.Site.Domain/Seo/SeoConfiguration.cs`).
 *
 * The property names are the **stored** configuration keys, not a naming choice.
 *
 * Both limits are **advisory**. `SeoFieldType.Validate` does not reject an over-length value, because a
 * search engine truncates an over-long title or description rather than rejecting it. They are a
 * threshold for an editing UI (or AI-generated content) to aim for, so the control renders them as a
 * counter and never as a `Validators.maxLength`.
 */
export class SeoConfiguration {
  /** 60 - Google's practical truncation point for a search result title. */
  'Seo.MetaTitleCharLimit': unknown = [60];

  /** 160 - the conventional SERP snippet length. */
  'Seo.MetaDescriptionCharLimit': unknown = [160];
}
