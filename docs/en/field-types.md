```json
//[doc-seo]
{
    "Description": "The field types available to a Dignite Site: the FlexFields built-ins, CKEditor rich text, and Site's own content reference, file and SEO types with their configuration and value shapes."
}
```

# Field Types

A field's type decides how its configuration is read, how its value is validated and whether its value can be indexed. Site uses the field types of [FlexFields](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/flex-fields/field-types.md) and adds three of its own.

| Name | Value | Indexable | From |
|---|---|---|---|
| `Text` | A string | Yes | FlexFields |
| `Number` | A number | Yes | FlexFields |
| `DateTime` | A date, a date and time, or a month | Yes | FlexFields |
| `Select` | One option value, or several | Yes | FlexFields |
| `Boolean` | `true` or `false` | Yes | FlexFields |
| `Tree` | One node value, or several | Yes | FlexFields |
| `Matrix` | A list of blocks, each of a block type with its own fields | No | FlexFields |
| `Table` | A list of rows with the same columns | No | FlexFields |
| `CKEditor` | Rich text, as HTML or Markdown | No | FlexFields CKEditor |
| `Content` | References to other contents | Yes | Site |
| `FileExplorer` | Files from the site's [file library](files.md) | No | Site |
| `Seo` | The SEO metadata of a content | No | Site |

A field is searchable only if its type is indexable **and** the content type marks its use `Searchable`; see [Arranging Fields](content-types-and-fields.md#arranging-fields).

The names and configuration keys below are stored in every field definition, so they never change. The `site_list_field_types` [MCP tool](mcp/tools.md#fields) lists the field types of a running site.

## FlexFields Field Types

The configuration of the FlexFields field types, such as `Text.CharLimit` or the options of a `Select`, is described in the FlexFields documentation:

* [Field Types](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/flex-fields/field-types.md): `Text`, `Number`, `DateTime`, `Select`, `Boolean`, `Tree`, `Matrix` and `Table`.
* [CKEditor Field Type](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/flex-fields/ckeditor.md): `CKEditor`. In Site, set `CKEditor.ImagesContainerName` to `images` to let editors insert images, which are [uploaded into the file library](files.md#ckeditor-image-upload).

## Content

A `Content` field references other contents of the site, such as the related posts of a post or the author of an article. The value is a list of content ids, even when the field takes a single content:

````json
["3a1f6c3e-0d5e-4bd4-9a39-4f7c2f9f8a11", "9b2d7e4a-5c1b-4e8f-b6a2-1c3d5e7f9a20"]
````

| Configuration key | Default | Description |
|---|---|---|
| `Content.ContentTypeId` | `null` | Restricts the admin UI's picker to contents of one content type. |
| `Content.Multiple` | `false` | Whether the field takes more than one content. |
| `Content.Placeholder` | `null` | The placeholder of the picker. |

The field validates only whether a required value is present: whether a referenced content exists is checked by the picker when it is chosen, and by the renderer when it is shown. The field's view in `Dignite.Site.Public.Web` shows only the referenced contents that are published, and silently skips the others. A `Content` field is indexed by the referenced ids, so contents can be filtered by what they reference.

## File

A `FileExplorer` field holds one or more files from the site's [file library](files.md), picked or uploaded in the admin UI. The value is a list of the files' descriptors, as the administration API returns them:

````json
[
  {
    "id": "6f0c9a5e-2b1d-4c3e-8f7a-9d0e1b2c3d4e",
    "containerName": "images",
    "blobName": "4f9e0c2a7b3d4e5f8a1b2c3d4e5f6a7b.jpg",
    "name": "cover.jpg",
    "mimeType": "image/jpeg",
    "size": 248133,
    "url": "/api/site-public/files/images/4f9e0c2a7b3d4e5f8a1b2c3d4e5f6a7b.jpg?__tenant=..."
  }
]
````

| Configuration key | Default | Description |
|---|---|---|
| `FileExplorer.FileContainerName` | None; required | The container the picker browses and uploads to: `images` or `attachments`. |
| `FileExplorer.UploadFileMultiple` | `false` | Whether the field takes more than one file. |

The `url` is the file's relative [address](files.md#addresses). In a template, `GetFileUrl` reads the first file's address of a field; see [Templates](rendering/templates.md#reading-field-values).

## SEO

The `Seo` field type bundles the SEO metadata of a content into one value. Every site's field library has one field of this type, named `seo`, that cannot be renamed or deleted; a new page's first content type includes it. The value is an object:

````json
{
  "metaTitle": "A Week in Hokkaido",
  "metaDescription": "Seven days, three cities and one very cold lake.",
  "ogImage": "/api/site-public/files/images/4f9e0c2a7b3d4e5f8a1b2c3d4e5f6a7b.jpg?__tenant=...",
  "noIndex": false
}
````

| Key | Used as |
|---|---|
| `metaTitle` | The `<title>` and `og:title` of the content. When it is empty, the first text field of the content type that has a value is used, then the slug, or the page's display name for a page's own content. |
| `metaDescription` | The `description` and `og:description`. When it is empty, the next text field that has a value is used, cut at 500 characters. |
| `ogImage` | The `og:image`: an image from the file library, which is cropped to 1200 x 630 and made absolute on the site's primary domain, or any absolute image URL, which is used as it is. When it is empty, there is no `og:image`. |
| `noIndex` | When `true`, the content gets a `noindex` robots tag and is left out of the sitemap, the feeds and the hreflang alternates of its translations. |

| Configuration key | Default | Description |
|---|---|---|
| `Seo.MetaTitleCharLimit` | `60` | The advised length of the meta title. |
| `Seo.MetaDescriptionCharLimit` | `160` | The advised length of the meta description. |

The two limits are advice for editors and AI clients; a longer value is not refused, since a search engine truncates a long title rather than rejecting it. See [SEO](seo/index.md) for how the metadata is written into a page's `<head>`.

## See Also

* [Content Types and Fields](content-types-and-fields.md)
* [Files](files.md)
* [SEO](seo/index.md)
