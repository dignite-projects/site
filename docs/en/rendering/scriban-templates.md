```json
//[doc-seo]
{
    "Description": "Write the templates of a Dignite Site in Scriban, stored in the database per tenant: the layout and site_head, versions, preview and rollback, the data and functions a template can use, the sandbox limits, and the admin API and MCP tools that manage them."
}
```

# Scriban Templates

A page can be rendered by a [Scriban](https://github.com/scriban/scriban) template instead of a Razor view. Unlike a [Razor template](templates.md), which is compiled with the application and written by its developers, a Scriban template is stored in the database, belongs to one tenant, and is written by the tenant or by an AI client over the [admin API](#managing-templates) or the [MCP tools](#mcp-tools). It runs in a sandbox: a template can read the data it is given and call a fixed set of functions, and nothing else.

Scriban templates are available when the application uses `Dignite.Site.Public.Web`. Both engines can be used on the same site, page by page.

## Choosing the Engine

A page's `Template` is a name, such as `Default` or `Blog/Index`. When a request is rendered, the name is looked up in this order:

1. A **published Scriban template** of the same name, in the database of the request's tenant.
2. Otherwise, the **Razor view** of that name; see [Where Templates Live](templates.md#where-templates-live).

There is no setting on the page: the engine follows from which of the two exists. Saving a template named `Blog/Index` takes over every page whose template is `Blog/Index`; deleting it, or having no published version, returns those pages to the Razor view of the same name. A template that has been saved but never published, or only staged, is not rendered.

Names are written like a page's `Template`: letters, digits, `.`, `_`, `-` and `/`, with no leading slash. A leading slash and surrounding spaces are removed when a name is saved or looked up.

## The Layout

The template named `layout` is special: it wraps every page that is rendered by a Scriban template. The page template is rendered first, and its output is available to the layout as `body`. A layout is a complete document, with `{{ body }}` where the page goes; see the [example](#example).

The layout applies to pages rendered by a Scriban template; a page rendered by a Razor view never goes through it. Once a layout is published, the page templates write only what goes inside `<body>`. Variables that a page template assigns are visible to the layout.

The layout must call `site_head`, which writes the head metadata of the page: the title, the description, the canonical URL, the robots tag, Open Graph, the Twitter card and the hreflang alternates. Without it, every page of the site would lose its [SEO](../seo/index.md) tags. So the rule is enforced twice:

* Saving a `layout` that never calls `site_head` is rejected.
* Rendering a page whose render never executed `site_head` fails with the error `Site:090005`, instead of serving a page without the tags.

`site_head` writes the same tags as the `<site-head-metadata>` tag helper of a Razor view. It takes two optional arguments, a title and a description: the title overrides the Open Graph title, and the description is used only when the content has none of its own. For example, `{{ site_head "Archive" }}`.

## Versions, Publishing and Rollback

A template is a name with a list of numbered versions. A version is never edited: saving a template adds the next version, and the older ones stay. One version at a time is **published**, and only that one is rendered.

* **Create** a template with its first version.
* **Save** a template to add a version. By default the new version is published at once. With `publish` off it is only staged: it is stored, and the site keeps rendering the published version.
* **Publish** any existing version to make it the live one. Publishing an older version is the rollback; publishing the staged one is the release.
* **Delete** a template to remove it with all of its versions.

Every version can carry a short comment, and the body of every version can be read back. A version can be seen before it is published; see [Previewing a Version](#previewing-a-version).

Saving parses the body. A body with a syntax error is rejected, and nothing is stored; the error lists each problem on a line of its own, as `(line,column): message`.

By default a body can be at most 256 KiB, and a comment 256 characters.

## Previewing a Version

Any saved version of a template, whether a staged draft, the published one or an older one, can be rendered on a path of the site without publishing it. The path is resolved as a request would be: `/about` or `/ja/blog/hello`, with a language prefix if the site uses them, and a leading slash is not required. The page or content at that path is rendered with the chosen version in place of the published template of the same name, inside the published `layout` if there is one. Everything else is the live site's: the other templates, the data, the functions and the limits.

Two cases are different when the previewed template is the `layout`:

* The layout is rendered around the published Scriban template of the page at the path.
* When that page is still rendered by a Razor view, the layout is rendered around an empty `body`.

A preview renders a saved version, so to see an edit, save it as a draft (`publish` off) and preview that version. The site keeps rendering the published version meanwhile; publish the draft when the preview is right, and publish an older version to go back.

A preview fails with one of these errors:

* `Site:090006` when no page answers the path.
* `Site:090007` when the application does not have `Dignite.Site.Public.Web`: a preview renders the page the way the site does, so it needs the rendering in the same application as the administration API.

A render that fails, through a run-time error, the `site_head` rule or a limit, fails the preview with the error the live page would give, naming the template, the line and the column.

You can preview in three ways:

* In the [admin UI](../admin-ui.md#screens), the **Templates** screen renders the selected version in a frame, for the path you type.
* Over the [admin API](#managing-templates), which returns the HTML with the page it was rendered for.
* Over MCP, with `site_preview_template`, so that an AI client can check its own draft before it publishes; see [MCP Tools](#mcp-tools).

## What a Template Sees

A template is given data, and only data: the page and the content being rendered, and the language. The names are in `snake_case`, and every date and time is a real date, so `date` functions and members such as `content.publish_time.year` work.

| Variable | Description |
|---|---|
| `page` | The resolved page: `id`, `name`, `display_name`, `route`, `template`, `is_home_page`, `parent_id`, `is_active` and the audit fields. |
| `content` | The resolved content, or empty when the request names the page itself, such as a list. It has `id`, `page_id`, `content_type_id`, `culture_name`, `slug`, `publish_time`, `status` (a lower-case text such as `"published"`), `url`, `fields`, `field_list` and the audit fields. |
| `content.fields` | The values of the content, keyed by the field names exactly as the site defined them: `content.fields.title`. A value has its JSON shape, and the members of an object inside a value are in `camelCase`, as over HTTP. |
| `content_type` | The content type of `content`, or empty when the request names the page itself. It has `id`, `page_id`, `name`, `display_name`, `description` and `fields`, each of which has `field_id`, `required`, `searchable`, `show_in_list`, `display_name` and `order`. |
| `field_list` | The fields of `content`, in the content type's order, without the SEO field. Each has `name`, `display_name`, `field_type_name`, `required` and `value`. An empty list when there is no content; the same list as `content.field_list`. |
| `culture` | The language of the request, such as `en` or `zh-Hans`. |
| `default_culture` | The site's default language. |
| `languages` | The site's languages, for a language switcher. Each has `culture_name`, `url`, `is_current` and `is_translation`, whether the current content exists in that language. |
| `filter_values`, `field_filters` | What the path gave the route's placeholders, with the keys as the route wrote them: `filter_values["publishTime:yyyy"]`. See [The View Model](templates.md#the-view-model). |
| `published_after`, `published_before` | The period that `publishTime` placeholders named, as dates, or empty. |
| `head` | The head metadata of the request, or empty: `meta_title`, `base_meta_title`, `meta_description`, `canonical_url`, `robots_content`, `og_image_url`, `og_type`, `twitter_card_type`, `hreflang_alternates` (each with `culture_name`, `url` and `path`) and `x_default_url`. |
| `body` | In the layout only: the rendered page template. |

### Output Encoding

Every value a template writes is HTML-encoded, as in a Razor view: `{{ content.fields.title }}` is safe even when the title holds markup. Three outputs are written as they are:

* the output of `site_head`,
* the output of `render_field`, which the field type has already encoded or sanitized for its kind of value,
* `body`, in the layout.

`html_raw` marks a value as trusted HTML, so that it is written as it is. It bypasses the field type's own sanitizing, so use `render_field` for rich text and keep `html_raw` for HTML that you control.

## Functions

Besides Scriban's own `string`, `array`, `math`, `date`, `regex` and the like, a template can call these functions. Their names cannot be reassigned.

| Function | Description |
|---|---|
| `site_head [title] [description]` | Writes the head tags; see [The Layout](#the-layout). |
| `t key [fallback]` | A localized text, in the language of the page; `fallback` when the key has no text. It can be piped: `{{ "Key" | t }}`. See [Localizing Template Texts](template-localization.md). |
| `url path [culture]` | The address of a path in a language. Only a path that starts with `~/` gets the language prefix and the application's path base; anything else is returned unchanged. The language of the request by default. |
| `file_url url width [height]` | The address of a stored image resized to the given size, from a file field's value. |
| `contents [options]` | Lists published contents; see below. |
| `render_field name [show_in_list]` | Renders a field of `content` with the view of its field type, as `<flex-field-view>` does. |
| `select_options page field` | The options of a `Select` field, as a list of `{ text, value }`. |
| `html_raw value` | Marks a value as trusted HTML; see [Output Encoding](#output-encoding). |

`contents` takes an object with these keys, all optional, and returns the page of contents with `items`, `total_count`, `max_result_count` and `current_page`:

| Key | Description |
|---|---|
| `page` | The name of the page to list. Omit it to list the current page, with the current route's `field_filters`, `published_after` and `published_before` applied. |
| `content_type` | The name of a content type of that page. |
| `max` | The page size, 20 by default and at most 100. |
| `page_no` | The 1-based page number, 1 by default. |
| `filter` | A text filter over the slug. |

A key that is not in the table is an error, and a `page` that does not exist gives an empty list. Contents are listed newest first, and only published ones. Each item has the shape of `content`, except that its `field_list` holds only the fields marked Show In List.

````
{{ news = contents { page: "news", max: 5 } }}
<ul>
{{ for item in news.items }}
    <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
{{ end }}
</ul>
````

## Limits

A template runs in a sandbox, so that a template written by a tenant or by an AI cannot take the application down or read what it should not.

* A template reaches only the variables and functions above. It cannot call into the application, read files, or `include` another template.
* The loops of one template run at most 10,000 iterations per outermost loop, nested loops counted together, and nested calls go at most 100 deep.
* A regular expression match is stopped after 100 ms.
* One template writes at most 2 Mi characters (2,097,152).
* Rendering one page, layout included, must finish within 3 seconds.

A template that exceeds a limit, or fails at run time, fails the request with an error that names the template and the line and column; a timeout is reported as a timeout, naming the template. A host can change the limits with `SiteScribanOptions`.

## Managing Templates

The admin API manages templates under `api/site-admin/templates`:

| Request | Permission | Description |
|---|---|---|
| `GET /` | `SiteAdmin.Templates` | Lists the templates, each with its published and latest version numbers. Takes an optional `filter` over the name. |
| `GET /{id}` | `SiteAdmin.Templates` | Reads one template. |
| `GET /by-name?name=...` | `SiteAdmin.Templates` | Reads a template by name; empty when there is none. |
| `POST /` | `SiteAdmin.Templates.Create` | Creates a template with `name`, `body`, an optional `comment` and `publish`, true by default. |
| `PUT /{id}` | `SiteAdmin.Templates.Update` | Saves the next version, with `body`, an optional `comment` and `publish`. |
| `POST /{id}/publish/{versionNumber}` | `SiteAdmin.Templates.Update` | Publishes a version, newer or older. |
| `GET /{id}/versions` | `SiteAdmin.Templates` | Lists the versions, newest first, without their bodies. |
| `GET /{id}/versions/{versionNumber}` | `SiteAdmin.Templates` | Reads one version with its body. |
| `GET /{id}/versions/{versionNumber}/preview?path=...` | `SiteAdmin.Templates` | Renders the version at the path; see [Previewing a Version](#previewing-a-version). Returns `html`, `path`, `pageName`, `cultureName`, `pageTemplate` (the page's own template, which tells when the previewed template is not the one the page normally uses) and `versionNumber`. |
| `GET /{id}/versions/{versionNumber}/preview.html?path=...` | `SiteAdmin.Templates` | The same, as a `text/html` document, for a browser authenticated with a cookie. It is not part of the API definition, so it has no generated proxy. |
| `DELETE /{id}` | `SiteAdmin.Templates.Delete` | Deletes the template with all of its versions. |

A syntax error, a `layout` without `site_head` and a name that is already taken are answered as business errors, with the lines and columns in the message.

### MCP Tools

The MCP server offers the same operations, addressed by template name: `site_list_templates`, `site_get_template`, `site_save_template`, `site_publish_template` and `site_delete_template`, and `site_preview_template`, which renders a version at a path. Saving needs both the create and the update permission, since the same call creates a template or adds a version. The recommended flow is to save as a draft (`publish` false), preview it, and then publish it; publishing an older version is the rollback. See [Tools](../mcp/tools.md#templates).

## Example

A Scriban equivalent of a minimal Razor `Default` view, which renders a content with its fields, or the list of the page's contents. Without a layout, the template is a complete document:

````
<!DOCTYPE html>
<html lang="{{ culture }}">
<head>
    <meta charset="utf-8" />
    <title>{{ head?.meta_title ?? page.display_name }}</title>
    {{ site_head }}
</head>
<body>
<h1>{{ page.display_name }}</h1>
{{ if content }}
    <p>{{ content.publish_time | date.to_string "%Y-%m-%d" }}</p>
    <hr />
    {{ for field in field_list }}
    <section>
        <h2>{{ field.display_name }}</h2>
        {{ render_field field.name }}
    </section>
    {{ end }}
{{ else }}
    {{ for key in (object.keys field_filters) }}<p>{{ key }}: {{ field_filters[key] }}</p>{{ end }}
    {{ list = contents { max: 20 } }}
    <ul>
    {{ for item in list.items }}
        <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
    {{ end }}
    </ul>
    {{ if list.total_count > list.items.size }}<p>{{ list.items.size }} / {{ list.total_count }}</p>{{ end }}
{{ end }}
</body>
</html>
````

The document around the page is the same for every page, so it belongs in the `layout` template:

````
<!DOCTYPE html>
<html lang="{{ culture }}">
<head>
    <meta charset="utf-8" />
    <title>{{ head?.meta_title ?? page.display_name }}</title>
    {{ site_head }}
</head>
<body>
<nav>{{ for lang in languages }}<a href="{{ lang.url }}">{{ lang.culture_name }}</a> {{ end }}</nav>
{{ body }}
</body>
</html>
````

Once the layout is published, the page template writes only what goes inside `<body>`, so the `Default` template shrinks to:

````
<h1>{{ page.display_name }}</h1>
{{ if content }}
    <p>{{ content.publish_time | date.to_string "%Y-%m-%d" }}</p>
    <hr />
    {{ for field in field_list }}
    <section>
        <h2>{{ field.display_name }}</h2>
        {{ render_field field.name }}
    </section>
    {{ end }}
{{ else }}
    {{ for key in (object.keys field_filters) }}<p>{{ key }}: {{ field_filters[key] }}</p>{{ end }}
    {{ list = contents { max: 20 } }}
    <ul>
    {{ for item in list.items }}
        <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
    {{ end }}
    </ul>
    {{ if list.total_count > list.items.size }}<p>{{ list.items.size }} / {{ list.total_count }}</p>{{ end }}
{{ end }}
````

Saved with these two templates, every page whose `Template` is `Default` renders through Scriban, and every other page keeps its Razor view.

## See Also

* [Templates](templates.md)
* [Multilingual Sites](languages.md)
* [Localizing Template Texts](template-localization.md)
* [SEO](../seo/index.md)
* [MCP Tools](../mcp/tools.md)
