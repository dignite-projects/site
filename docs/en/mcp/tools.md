```json
//[doc-seo]
{
    "Description": "Reference of the site_* MCP tools of Dignite Site: the schema, languages, pages, content types, fields, contents, path resolution, the theme and its releases, and files, with their parameters and permissions."
}
```

# Tools

Every tool of Site is named `site_*`, and its resources, `site://schema` and `site://theme-guide`, use the `site://` scheme, so they never collide with the tools of other modules on the same server. Pages, content types, fields and contents are addressed by their names, from the schema; files and directories by the ids the file tools return.

The descriptions below are summaries; a client reads the full description of each tool and parameter from `tools/list`. Parameters marked optional can be left out; for the update tools, a parameter left out keeps its current value.

## Schema

| Tool | Permission | Description |
|---|---|---|
| `site_get_schema` | `SiteAdmin.Pages`, `SiteAdmin.ContentTypes` and `SiteAdmin.Fields` | Returns the schema: the enabled languages (the first is the default), the primary domain, every page with its route, and every content type with its fields, their types, configuration, required flags and descriptions. |

The `site://schema` resource returns the same document, with the same permissions, to a client that supports resources. A client should read one of them before calling anything else.

## Languages

| Tool | Permission | Description |
|---|---|---|
| `site_set_languages` | `SiteAdmin.Languages.Update` | Sets the languages the site serves, with `enabledLanguages`, the whole list in order. The first is the default language. |

A site serves only `en` until its languages are set, so a client that writes content in another language sets them first. The tool replaces the list: read the current one from the schema's `enabledLanguages` and send it back with the change. Tags are checked and stored in their canonical form, such as `zh-Hans`; a tag that is not a language, or an empty list, is refused.

Changing the languages moves addresses. The default language's pages have no prefix, so a new default takes the unprefixed addresses and the old one's pages move under `/{language}/`; and a language left out is no longer served, though its contents are kept and come back when the language is added again. The application has to load `SiteSettingManagementModule` from the `Dignite.Site.SettingManagement` package, which stores the setting through ABP's Setting Management.

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
| `template` | The template that renders the page: the path of a [Scriban template](../rendering/scriban-templates.md) of the theme, or else the name of a Razor view; `Default` when the site does not use another. |
| `parent` | Optional. The name of the page to organize it under in the admin UI. |
| `isActive` | Optional, `true` by default. |

`site_update_page` takes `page`, the page's current name, and any of `name`, `displayName`, `route`, `template`, `parent` (an empty string makes it a top-level page) and `isActive`. `site_delete_page` takes `page`; it fails while the page has child pages.

## Content Types

| Tool | Permission | Description |
|---|---|---|
| `site_create_content_type` | `SiteAdmin.ContentTypes.Create` | Defines a shape of content under a page, from fields that already exist in the library. |
| `site_update_content_type` | `SiteAdmin.ContentTypes.Update` | Updates a content type. Passing `fields` replaces the whole arrangement; an empty list clears it. |
| `site_delete_content_type` | `SiteAdmin.ContentTypes.Delete` | Deletes a content type that no content uses. Destructive. |

`site_create_content_type` takes `page`, `name` (unique within the page), `displayName`, an optional `description` and an optional `fields` list. `site_update_content_type` takes `page`, `contentType` (the current name) and any of `name`, `displayName`, `description` and `fields`. `site_delete_content_type` takes `page` and `contentType`. Creating and updating return the content type with its fields by name, in order.

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
| `site_list_field_types` | `SiteAdmin.Fields` | Lists the field types, with the `configurationKeys` each one understands (name, type, default, description, whether it is required) and the `valueShape` of a type whose value is an object, such as `Seo`. |
| `site_create_field` | `SiteAdmin.Fields.Create` | Adds a field to the library. |
| `site_update_field` | `SiteAdmin.Fields.Update` | Updates a field everywhere it is used. Cannot rename it. |
| `site_rename_field` | `SiteAdmin.Fields.Rename` | Renames a field and moves the stored value of every content to the new name. Destructive. |
| `site_delete_field` | `SiteAdmin.Fields.Delete` | Deletes a field and removes its values from every content. Destructive. |

`site_create_field` takes `name`, `displayName`, `fieldTypeName`, an optional `description` and an optional `configuration`, whose keys are the field type's `configurationKeys`; a key it does not list, a missing required key, or a value that cannot be read as the listed kind is refused (a number written as text, such as `"100"`, reads as a number and is accepted). See [Field Types](../field-types.md). `site_update_field` takes `field` (the name) and any of `displayName`, `fieldTypeName`, `description` and `configuration`, which replaces the whole configuration. `site_rename_field` takes `field` and `newName`; `site_delete_field` takes `field`.

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

The write and list tools say where a content is, by names, and do not send its values back: `site_create_content` and `site_update_content` return the `page`, `contentType`, `cultureName`, `slug`, `status` and `publishTime`, and `storedFields`, the names of the values that were stored, so a value the content type does not declare shows as missing from it; `site_list_contents` returns the same for each row, with the values only when `includeFieldValues` is true. `site_get_content` reads the values.

## Routing

| Tool | Permission | Description |
|---|---|---|
| `site_resolve_path` | None | Resolves a path, such as `/zh-Hans/blog/my-trip`, to its page and content, as a visitor's request would be. Takes `path`. |

It answers with where the path leads - `matched`, `kind`, `cultureName`, the `page` and `contentType` names, the `slug` and `url` of the content, and the `filterValues` of a partial match - not with the content; `site_get_content` reads that. It sees only what is published, so a match means something is live at that address. See [Resolving a Request](../pages-and-routing.md#resolving-a-request).

## Theme

| Tool | Permission | Description |
|---|---|---|
| `site_get_theme_guide` | `SiteAdmin.Themes` | Returns [how to write the theme](../rendering/scriban-templates.md) as Markdown: the variables a template sees, every function it can call, layouts, partials, static files and the limits of the sandbox. The `site://theme-guide` resource returns the same document. A client should read one of them before it writes a template. |
| `site_list_theme_files` | `SiteAdmin.Themes` | Lists the files of the [theme](../rendering/scriban-templates.md#the-theme)'s working copy, each with its status against the live release, and returns the default layout and the live release's number. |
| `site_get_theme_file` | `SiteAdmin.Themes` | Reads one file, with its syntax errors. Takes `path` and an optional `release`, a release number, to read the file as that release has it. |
| `site_save_theme_file` | `SiteAdmin.Themes.Edit` | Creates or overwrites a file of the working copy, with `path` and `body`, and returns its syntax errors. The site does not change. |
| `site_rename_theme_file` | `SiteAdmin.Themes.Edit` | Moves a file, with `path`, `newPath` and `updateReferences`, true by default, which rewrites what names it. |
| `site_delete_theme_file` | `SiteAdmin.Themes.Edit` | Removes a file from the working copy, and returns what used it. Destructive. |
| `site_update_theme` | `SiteAdmin.Themes.Edit` | Sets the default layout, with `defaultLayout`, a template path or an empty string for none. |
| `site_check_theme` | `SiteAdmin.Themes` | [Checks](../rendering/scriban-templates.md#checking-the-theme) the working copy, lists what publishing would change, and returns the `fingerprint` a publish needs. |
| `site_preview` | `SiteAdmin.Themes` | [Previews](../rendering/scriban-templates.md#previewing) the page at `path` - which may carry `?page=2` for the page of a paged list - with the working copy, or with an optional `release`, and returns the HTML with `pageName`, `cultureName`, `pageTemplate` and `layout`. An optional `maxHtmlLength` returns only the start of the HTML. |
| `site_publish_theme` | `SiteAdmin.Themes.Publish` | Publishes the working copy as a new release and makes it live, with the check's `fingerprint` and an optional `comment`. |
| `site_list_releases` | `SiteAdmin.Themes` | Lists the releases, newest first, with which one is live. |
| `site_set_live_release` | `SiteAdmin.Themes.Publish` | Makes the release with the number `release` live; an older one is the rollback. |
| `site_restore_release` | `SiteAdmin.Themes.Edit` | Replaces the working copy with the release with the number `release`. Nothing goes live. Destructive. |

The theme is not part of the schema: its files are addressed by path, and `site_list_theme_files` returns them; releases are addressed by number. Saving changes only the working copy. Save, check and fix every error the check reports, preview a path, and then publish with the check's fingerprint; a publish is refused when the working copy changed since the check. The HTML of `site_preview` is cut at 32 KiB, or at `maxHtmlLength` when that is smaller: then `isTruncated` is true and `htmlLength` is the full length. A client that only needs to know that a page renders asks for a few thousand characters.

## Files

| Tool | Description |
|---|---|
| `site_list_file_containers` | Lists the containers the client may use, with each one's purpose, largest upload and allowed file types. Call it before the other file tools. |
| `site_list_files` | Lists the files of a container, newest first: the user's own, or everyone's with `SiteAdmin.Contents`. Takes `containerName` and the optional `directoryId`, `filter`, `skipCount` and `maxResultCount` (at most 100). |
| `site_get_file` | Reads one file by `id`, with its `url`. |
| `site_create_upload_url` | Returns a one-time link, good for ten minutes, and the commands that send a file to it (one for POSIX shells and cmd.exe, one for PowerShell), from `containerName`, `fileName` and an optional `directoryId`. The way to upload a large file: the bytes go from the machine the file is on to the site and never through the client. |
| `site_upload_file` | Uploads a small file from `containerName`, `fileName` (with its extension), `contentBase64` (or a `data:` URL) and an optional `directoryId`, and returns it with its `url`, the value to put in a content field. |
| `site_update_file` | Renames a file (`name`) or moves it (`directoryId`, or `moveToRoot`), by `id`. |
| `site_delete_file` | Deletes a file by `id`. Anything that links to it stops working. Destructive. |
| `site_list_directories` | Returns the user's directory tree in a container, by `containerName`. |
| `site_create_directory` | Creates a directory in a container, by `containerName`, `name` and an optional `parentId`. |

The file tools need no permission to be listed; each call checks the permissions of the container, the `SiteAdmin.Contents.*` permissions. Uploads are capped at the smaller of the container's limit and `SiteMcpFileOptions.MaxUploadSize`, 5 MB by default. See [Files](../files.md#mcp).

## See Also

* [MCP Server](index.md)
* [Connecting AI Clients](connecting-clients.md)
