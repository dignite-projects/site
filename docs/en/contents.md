```json
//[doc-seo]
{
    "Description": "Contents of a Dignite Site: one row per language, slugs, translations, drafts, scheduled publishing and archiving, and how field values are validated."
}
```

# Contents

A **content** is one record of a site: an article, a product, the text of an "about" page. It belongs to a [page](pages-and-routing.md), which decides its URL, and to one of that page's [content types](content-types-and-fields.md), which decides its fields.

## Content Properties

| Property | Description |
|---|---|
| `PageId`, `ContentTypeId` | The page and the content type. The content type must belong to the page (`Site:040002`); a content can move to another content type of the same page. |
| `CultureName` | The language of this content, such as `en`, `zh-Hans` or `en-GB`. |
| `Slug` | The segment of the URL that identifies the content beneath its page, such as `my-trip`. Empty for the page's own content. |
| `Status` | `Draft`, `Published` or `Archived`. |
| `PublishTime` | When the content is published. |
| Field values | The values of the content type's fields, keyed by the field names. |

## Languages and Translations

A content is **one language**: a content in two languages is two contents. The language tag is stored in its canonical .NET form, so `zh-hans` and `zh-Hans` are the same language; a tag .NET does not recognize is refused (`Site:040007`). Write contents only in the site's languages, the `Site.EnabledLanguages` [setting](settings.md): a content in any other language is never routed.

The contents of one page and content type that share a slug are the **translations** of each other: `/blog/my-trip` and `/ja/blog/my-trip` are the English and Japanese versions of one post. There is no separate translation group to maintain, but it means a content keeps the same slug in every language. The [hreflang alternates](seo/index.md#hreflang-alternates) and the [language switcher](rendering/languages.md#language-switcher) are derived from these groups.

Deleting a content deletes that language only.

## Slugs

A slug is unique within its page and language (`Site:040001`). Whether a slug is required, optional or not allowed depends on the page's [route](pages-and-routing.md#route-templates).

A slug is normalized before it is stored: it is lowercased, spaces and punctuation become separators, and Unicode letters and digits are kept as they are, so `我的旅行` and `Мой отпуск` are valid slugs. Site does not transliterate; a site that wants romanized slugs passes them in.

Changing a content's slug moves its URL, and links to the old one stop working.

## Publishing

| Status | Served to visitors |
|---|---|
| `Draft` | Never. A draft is not routed and is left out of the sitemap and the feeds. |
| `Published` | Once its `PublishTime` has passed. |
| `Archived` | Never. The content is kept, but not routed and not in the sitemap. |

To **schedule** a content, publish it with a future `PublishTime`: it is hidden until that time, with no background job involved. A draft cannot have a future publish time (`Site:040003`). To cancel a schedule, set the status back to `Draft` together with a publish time that is not in the future.

## Field Values

Field values are validated against the content type when a content is saved, with the rules of each field's type: a required value that is missing, a text longer than its limit, an option a select does not have. All the errors of a save are returned together, each naming its field.

A value for a field that the content type does not arrange is dropped. A value of a field marked `Searchable` in the content type is indexed in the same transaction, so a content can be filtered by it as soon as it is saved.

## Managing Contents

Contents are managed with the `SiteAdmin.Contents.*` permissions:

* In the Angular [admin UI](admin-ui.md), under **Site > Contents**.
* By an AI client, with the `site_*_content` [MCP tools](mcp/tools.md#contents).
* With the administration API, `/api/site-admin/contents`.

The public read API, `/api/site-public/contents`, serves published contents only.

## See Also

* [Pages and Routing](pages-and-routing.md)
* [Content Types and Fields](content-types-and-fields.md)
* [Field Types](field-types.md)
