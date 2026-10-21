```json
//[doc-seo]
{
    "Description": "The field library and content types of a Dignite Site: define a field once, arrange it into content types with per-type required, searchable and list flags, and rename or delete fields safely."
}
```

# Content Types and Fields

The shape of a site's contents is defined in two layers:

* A **field** is defined once, in the site's **field library**: what it is, its field type and its configuration.
* A **content type** belongs to one [page](pages-and-routing.md) and arranges fields from the library into one shape of content, saying how this type uses each field.

The same `title` field can be pulled into the article, gallery and video content types of a blog and into the content type of an "about" page. Raising its character limit is one change in the library, and every type that uses it follows. Content types are not shared across pages; reuse happens at the field level.

These definitions are also the contract with the AI client: they are the validation gate of every write, and the descriptions of fields and content types are the briefing a model reads before it writes. See [MCP Server](mcp/index.md).

## Fields

| Property | Description |
|---|---|
| `Name` | A machine name, unique in the site, such as `title` or `cover`. It is the key that every content stores the field's value under. Lowercase letters and digits, with `-` or `_` as separators. |
| `DisplayName` | The human-readable name. |
| `Description` | What belongs in the field. An AI client reads it while it writes content, so write it for that reader. |
| `FieldTypeName` | The [field type](field-types.md), such as `Text`, `Select` or `Seo`. |
| `Configuration` | The field type's configuration: a length limit, the options of a select, the container of a file field, and so on. |
| `GroupName` | An optional group, to keep a large library navigable. It has no effect at runtime. |

Fields are defined with the `SiteAdmin.Fields.*` permissions, in the admin UI or with the `site_*_field` MCP tools.

### Changing a Field's Type

Changing `FieldTypeName` keeps the stored values as they are and rebuilds the search index from them. Values are not converted, so a value the new type cannot read may fail validation the next time its content is saved.

### Renaming a Field

A field's `Name` is the key its values are stored under, so renaming a field is a **data migration**: the field definition and the values of every content are changed in one transaction. Renaming needs its own permission, `SiteAdmin.Fields.Rename`, and any template or external front end that reads the field by its old name has to be changed too.

### Deleting a Field

Deleting a field also removes its values from every content, and cannot be undone. The content types that arrange the field keep their reference to it; it is skipped from then on.

### The SEO Field

Every site's library is seeded with one field named `seo`, of the [SEO field type](field-types.md#seo). It is a platform preset: it cannot be renamed (`Site:030003`) or deleted (`Site:030002`), but its display name and configuration can be changed. Remove it from a content type's arrangement to leave it out of that type.

## Content Types

| Property | Description |
|---|---|
| `PageId` | The page the type belongs to. A content type never moves to another page. |
| `Name` | A machine name, unique within the page, such as `post-article`. |
| `DisplayName` | The human-readable name. |
| `Description` | What this type is for and when to choose it over the others under the same page. It is how "post a news item" lands on the right type, so be concrete. |
| `Fields` | The ordered arrangement of fields, described below. |

### Arranging Fields

Each entry of a content type's arrangement refers to one field of the library and says how **this type** uses it:

| Property | Description |
|---|---|
| `FieldId` | The field. A field can appear only once in a type (`Site:020002`). |
| `Required` | Whether a content of this type must have a value. |
| `Searchable` | Whether the value is indexed, so contents can be filtered by it: by a [route placeholder](pages-and-routing.md#field-placeholders), a `content-list` filter in a [template](rendering/templates.md) or the administration API. Field types whose values cannot be indexed, such as rich text, are never indexed. |
| `ShowInList` | Whether list views show the field: the admin UI's content list, and the items a `content-list` tag helper hands to its partial view. |
| `DisplayName` | An optional display name for this type only. |
| `Order` | The position of the field in the type. |

The same field can be required and searchable in one type and optional and unindexed in another.

Removing a field from a type's arrangement stops its values being read for contents of that type, but does not delete them. A content type that any content still uses cannot be deleted; move or delete those contents first.

## Composite Fields

The `Matrix` and `Table` field types hold fields of their own: the blocks of a Matrix and the columns of a Table. Their sub-fields are defined in the field's configuration, not in the library, and cannot themselves be `Matrix` or `Table` (`Site:030004`).

## See Also

* [Field Types](field-types.md)
* [Contents](contents.md)
* [Pages and Routing](pages-and-routing.md)
