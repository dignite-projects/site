```json
//[doc-seo]
{
    "Description": "Reference of the site_* MCP tools of Dignite Site: the schema, pages, content types, fields, contents, path resolution, templates and files, with their parameters and permissions."
}
```

# Tools

Every tool of Site is named `site_*`, and its one resource is `site://schema`, so they never collide with the tools of other modules on the same server. Pages, content types, fields and contents are addressed by their names, from the schema; files and directories by the ids the file tools return.

The descriptions below are summaries; a client reads the full description of each tool and parameter from `tools/list`. Parameters marked optional can be left out; for the update tools, a parameter left out keeps its current value.

## Schema

| Tool | Permission | Description |
|---|---|---|
| `site_get_schema` | `SiteAdmin.Pages`, `SiteAdmin.ContentTypes` and `SiteAdmin.Fields` | Returns the schema: the enabled languages (the first is the default), the primary domain, every page with its route, and every content type with its fields, their types, configuration, required flags and descriptions. |

The `site://schema` resource returns the same document, with the same permissions, to a client that supports resources. A client should read one of them before calling anything else.

## Pages

| Tool | Permission | Description |
|---|---|---|
| `site_create_page` | `SiteAdmin.Pages.Create` | Creates a page, with one content type named after it that carries the SEO field. |
| `site_update_page` | `SiteAdmin.Pages.Update` | Updates a page. Changing the route changes the URL of every content beneath it. |
| `site_delete_page` | `SiteAdmin.Pages.Delete` | Deletes a page with all of its content types and contents, in every language. Destructive. |

`site_create_page` takes:

| Parameter | Description |
|---|---|
| `name` | The machine name, unique in the site, such as `blog`. |
| `displayName` | The human-readable name. |
| `route` | The route template, such as `/blog/{slug}`. See [Route Templates](../pages-and-routing.md#route-templates). |
| `template` | The view that renders the page; `Default` when the site does not use another. |
| `parent` | Optional. The name of the page to organize it under in the admin UI. |
| `isActive` | Optional, `true` by default. |

`site_update_page` takes `page`, the page's current name, and any of `name`, `displayName`, `route`, `template`, `parent` (an empty string makes it a top-level page) and `isActive`. `site_delete_page` takes `page`; it fails while the page has child pages.

## Content Types

| Tool | Permission | Description |
|---|---|---|
| `site_create_content_type` | `SiteAdmin.ContentTypes.Create` | Defines a shape of content under a page, from fields that already exist in the library. |
| `site_update_content_type` | `SiteAdmin.ContentTypes.Update` | Updates a content type. Passing `fields` replaces the whole arrangement; an empty list clears it. |
| `site_delete_content_type` | `SiteAdmin.ContentTypes.Delete` | Deletes a content type that no content uses. Destructive. |

`site_create_content_type` takes `page`, `name` (unique within the page), `displayName`, an optional `description` and an optional `fields` list. `site_update_content_type` takes `page`, `contentType` (the current name) and any of `name`, `displayName`, `description` and `fields`. `site_delete_content_type` takes `page` and `contentType`.

Each entry of `fields` is:

| Property | Description |
|---|---|
| `field` | The name of a field in the library. |
| `required` | Whether a content of this type must have a value. |
| `searchable` | Whether the value is indexed for filtering. |
| `showInList` | Whether list views show the field. |
| `displayName` | Optional. A display name for this type only. |
| `order` | The position in the type; lower comes first. |

See [Arranging Fields](../content-types-and-fields.md#arranging-fields).

## Fields

| Tool | Permission | Description |
|---|---|---|
| `site_list_fields` | `SiteAdmin.Fields` | Lists the whole field library, fields no content type uses included. Takes an optional `filter` over the name and display name. |
| `site_list_field_types` | `SiteAdmin.Fields` | Lists the field types, with the `valueShape` of a type whose value is an object, such as `Seo`. |
| `site_create_field` | `SiteAdmin.Fields.Create` | Adds a field to the library. |
| `site_update_field` | `SiteAdmin.Fields.Update` | Updates a field everywhere it is used. Cannot rename it. |
| `site_rename_field` | `SiteAdmin.Fields.Rename` | Renames a field and moves the stored value of every content to the new name. Destructive. |
| `site_delete_field` | `SiteAdmin.Fields.Delete` | Deletes a field and removes its values from every content. Destructive. |

`site_create_field` takes `name`, `displayName`, `fieldTypeName`, an optional `description` and an optional `configuration`, whose keys depend on the field type; see [Field Types](../field-types.md). `site_update_field` takes `field` (the name) and any of `displayName`, `fieldTypeName`, `description` and `configuration`, which replaces the whole configuration. `site_rename_field` takes `field` and `newName`; `site_delete_field` takes `field`.

## Contents

| Tool | Permission | Description |
|---|---|---|
| `site_list_contents` | `SiteAdmin.Contents` | Lists contents, newest first, drafts included. |
| `site_get_content` | `SiteAdmin.Contents` | Reads one content, with its field values in the shape `site_update_content` accepts. |
| `site_create_content` | `SiteAdmin.Contents.Create` | Creates a content. |
| `site_update_content` | `SiteAdmin.Contents.Update` | Updates a content. Passing `fieldValues` replaces all of its values. |
| `site_delete_content` | `SiteAdmin.Contents.Delete` | Deletes a content in one language; its translations stay. Destructive. |

A content is addressed by `page`, `cultureName` and `slug`, an empty string for the page's own content.

`site_create_content` takes:

| Parameter | Description |
|---|---|
| `page` | The page's name. |
| `contentType` | The name of one of the page's content types. |
| `cultureName` | One of the schema's enabled languages. |
| `slug` | Required, even when empty. A title is accepted and turned into a slug. See [Slugs](../contents.md#slugs). |
| `fieldValues` | Optional. The values, keyed by field name. |
| `status` | Optional. `Draft`, the default, or `Published`. |
| `publishTime` | Optional, now by default. A `Published` content with a future time is scheduled. |

`site_update_content` takes `page`, `cultureName` and `slug`, and any of `newSlug`, `fieldValues`, `status`, `publishTime` and `contentType`, which moves the content to another content type of the page. To change one value, read the content with `site_get_content` and send all of its values back with the change.

`site_list_contents` takes the optional filters `page`, `contentType` (which needs `page`), `cultureName`, `status` and `filter` (over the slug only), and `skipCount` and `maxResultCount`, 20 by default.

## Routing

| Tool | Permission | Description |
|---|---|---|
| `site_resolve_path` | None | Resolves a path, such as `/zh-Hans/blog/my-trip`, to its page and content, as a visitor's request would be. Takes `path`. |

It sees only what is published, so a match means something is live at that address. See [Resolving a Request](../pages-and-routing.md#resolving-a-request).

## Templates

| Tool | Permission | Description |
|---|---|---|
| `site_list_templates` | `SiteAdmin.Templates` | Lists the [Scriban templates](../rendering/scriban-templates.md), each with its published and latest version numbers. Takes an optional `filter` over the name. |
| `site_get_template` | `SiteAdmin.Templates` | Reads a template: its summary, the body of one version, and the list of its versions. |
| `site_save_template` | `SiteAdmin.Templates.Create` and `SiteAdmin.Templates.Update` | Creates the template, or adds a version to it. A syntax error is refused with its lines and columns. |
| `site_preview_template` | `SiteAdmin.Templates` | Renders a version of a template at a path of the site without publishing it, and returns the HTML. See [Previewing a Version](../rendering/scriban-templates.md#previewing-a-version). |
| `site_publish_template` | `SiteAdmin.Templates.Update` | Makes a version the live one; an older version is the rollback. |
| `site_delete_template` | `SiteAdmin.Templates.Delete` | Deletes a template with all of its versions; the pages that used its name render through the Razor view of that name again. Destructive. |

Templates are not part of the schema: a template is addressed by its name, and `site_list_templates` returns the names. A page uses the published template whose name is the page's `template`; the template named `layout` wraps every page and must call `site_head`.

`site_get_template` takes `template` and an optional `version`, and returns the published version's body when `version` is left out (the latest, with `isPublished` false, when nothing is published). `site_save_template` takes `template`, `body`, an optional `comment` and `publish`, true by default; with `publish` false the version is staged and the site keeps rendering the published one. `site_preview_template` takes `template`, `path` and an optional `version`, the published one by default (the latest when nothing is published), and returns the rendered HTML with `pageName`, `cultureName`, `pageTemplate` (the page's own template), `version` and `isPublished`. The HTML is cut at 32 KiB: then `isTruncated` is true and `htmlLength` is the full length. Save as a draft (`publish` false), preview the version, and publish it with `site_publish_template` when it is right; publishing an older version is the rollback. `site_publish_template` takes `template` and `version`; `site_delete_template` takes `template`.

## Files

| Tool | Description |
|---|---|
| `site_list_file_containers` | Lists the containers the client may use, with each one's purpose, largest upload and allowed file types. Call it before the other file tools. |
| `site_list_files` | Lists the files of a container, newest first: the user's own, or everyone's with `SiteAdmin.Contents`. Takes `containerName` and the optional `directoryId`, `filter`, `skipCount` and `maxResultCount` (at most 100). |
| `site_get_file` | Reads one file by `id`, with its `url`. |
| `site_upload_file` | Uploads a file from `containerName`, `fileName` (with its extension), `contentBase64` (or a `data:` URL) and an optional `directoryId`, and returns it with its `url`, the value to put in a content field. |
| `site_update_file` | Renames a file (`name`) or moves it (`directoryId`, or `moveToRoot`), by `id`. |
| `site_delete_file` | Deletes a file by `id`. Anything that links to it stops working. Destructive. |
| `site_list_directories` | Returns the user's directory tree in a container, by `containerName`. |
| `site_create_directory` | Creates a directory in a container, by `containerName`, `name` and an optional `parentId`. |

The file tools need no permission to be listed; each call checks the permissions of the container, the `SiteAdmin.Contents.*` permissions. Uploads are capped at the smaller of the container's limit and `SiteMcpFileOptions.MaxUploadSize`, 5 MB by default. See [Files](../files.md#mcp).

## See Also

* [MCP Server](index.md)
* [Connecting AI Clients](connecting-clients.md)
