```json
//[doc-seo]
{
    "Description": "Render a Dignite Site with Razor templates: where templates live per tenant, the view model, reading field values, content lists, head metadata, and rendering from an external front end."
}
```

# Templates

`Dignite.Site.Public.Web` renders a site in an ASP.NET Core MVC application. Every request that no other endpoint of the application claims is [resolved](../pages-and-routing.md#resolving-a-request) to a page and, where the path names one, a content; the MVC view that the page's `Template` names then renders it.

Templates are Razor views, compiled with the application and deployed with it. They are written by the application's developers, never by tenants or AI clients: a Razor view runs arbitrary C#. What tenants and AI clients change is data (pages, contents, fields), which templates render.

## Where Templates Live

A page's `Template` is a view name, such as `Default` or `Blog/Index`. It is looked up in this order:

| Request | Locations |
|---|---|
| The host's site (no tenant) | `/Sites/{template}.cshtml`, `/Sites/Shared/{template}.cshtml` |
| A tenant's site | `/Sites/{tenantName}/{template}.cshtml`, `/Sites/{tenantName}/Shared/{template}.cshtml` |
| Then, for both | The usual MVC locations: `/Views/SiteRender/{template}.cshtml`, `/Views/Shared/{template}.cshtml` |

So `Views/Shared/` holds the templates every site shares, and a tenant overrides any of them, or adds its own, under `Sites/{tenantName}/` with the same relative path. A tenant never falls back to the host's `/Sites/` views. The same lookup applies to the partial views a template renders.

A template that cannot be found fails the request with ASP.NET Core's usual view-not-found error; it is not turned into a `404`. A path that resolves to nothing is answered with `404`, which the application's status code pages render.

> ABP's theme assigns a layout to every view through its `_ViewStart.cshtml`. A template that writes the whole document itself, as the examples here do, sets `Layout = null`. A template can just as well use a layout of the application.

The view runs with `CultureInfo.CurrentCulture` and `CurrentUICulture` set to the language of the page, so dates, numbers and localized texts follow the URL. See [Multilingual Sites](languages.md) and [Localizing Template Texts](template-localization.md).

## Setup

Templates use the tag helpers of Site and of FlexFields. In the application's `_ViewImports.cshtml`:

````cshtml
@using Dignite.Site.Public.Routing
@using Dignite.Site.Public.TagHelpers
@using Dignite.Site.Public.Templating
@using Dignite.Site.Public.Localization
@addTagHelper *, Dignite.Abp.FlexFields.Web
@addTagHelper *, Dignite.Site.Public.Web
````

## The View Model

Every template receives a `SiteRenderViewModel`:

| Property | Description |
|---|---|
| `Page` | The resolved page: its `Name`, `DisplayName`, `Route`, `Template` and so on. |
| `CultureName` | The language of the request. |
| `Content` | The resolved content, or `null` when the request names the page itself. |
| `FilterValues` | Every value the path gave the route's placeholders, as written, such as `{"publishTime:yyyy": "2026"}`. |
| `FieldFilters` | The values of field placeholders, keyed by field name, ready to filter a list. |
| `PublishedAfter`, `PublishedBefore` | The period that `publishTime` placeholders named, such as the whole of August 2026 for `/news/2026/08`. |
| `HeadMetadata` | The `<head>` metadata of the request; see [The Head](#the-head). |

One template serves the page and the contents beneath it, and branches on `Content`:

````cshtml
@model SiteRenderViewModel
@if (Model.Content != null)
{
    @* a content: /blog/my-trip, or the page's own content at /about *@
}
else
{
    @* the page itself: /blog, or a filtered view such as /blog/2026-08 *@
}
````

`Model.Content` has three properties: `Content`, the content itself with its `Slug`, `PublishTime`, `Url` and field values; `ContentType`, its content type; and `Fields`, its field values with their definitions, in the content type's order, without the SEO field.

## Reading Field Values

To render every field with the view of its field type, loop over `Fields` with FlexFields' tag helper:

````cshtml
@foreach (var field in Model.Content.Fields)
{
    <flex-field-view field="@field" />
}
````

A template usually lays fields out itself instead, reading them by name. Field names are each site's own data, so they are written in that site's templates. The extension methods of `Dignite.Site.Public.Templating` read a value whatever shape it arrives in, and read a missing or mistyped value as empty rather than throwing:

````cshtml
@{
    var content = Model.Content!;
    var cover = content.GetFileUrl("cover");
}
<article>
    <h1>@content.GetText("title")</h1>
    @if (cover != null)
    {
        <img src="@SiteFileUrl.Sized(cover, 1200)" alt="" />
    }
    @foreach (var tag in content.GetSelectedOptions("tags"))
    {
        <span>@tag.Text</span>
    }
</article>
````

| Method | Returns |
|---|---|
| `GetText(fieldName)` | The value as text; the first value of a field with several. |
| `GetTexts(fieldName)` | Every value as text, in stored order. |
| `GetFileUrl(fieldName)` | The address of the first file of a [file field](../field-types.md#file). |
| `GetSelectedOptions(fieldName)` | The chosen options of a `Select` field, with their current labels. |
| `GetMatrixBlocks(fieldName)` | The blocks of a `Matrix` field; read each block's values with `GetText` and `GetFileUrl`. |

They are defined on both `ContentRenderViewModel` and `ContentDto`. To render a `CKEditor` field, or any field with its own view, use `<flex-field-view>` for that field.

## Lists

A request for the page itself carries no list: the template asks for one with the `content-list` tag helper, which queries the page's published contents and renders them with a partial view:

````cshtml
<content-list page="@Model.Page"
              culture-name="@Model.CultureName"
              field-filters="@Model.FieldFilters"
              published-after="@Model.PublishedAfter"
              published-before="@Model.PublishedBefore"
              current-page="@currentPage"
              max-result-count="10"
              partial-name="_PostList" />
````

| Attribute | Description |
|---|---|
| `page` | The page to list. |
| `page-name` | A page to list by its name, when `page` is not given: a "latest news" block on the home page, say. |
| `culture-name` | The language to list. |
| `content-type-id` | Only contents of this content type. |
| `field-filters` | Filters by field values, keyed by field name. Only fields the content type marks `Searchable` filter; others are ignored. |
| `published-after`, `published-before` | Bounds on the publish time. |
| `filter` | A text filter over the slug. |
| `max-result-count` | The page size, 20 by default. |
| `current-page` | The 1-based page number, 1 by default. |
| `partial-name` | The partial view that renders the list. Required. |

Contents are listed newest first. The partial receives a `ContentListRenderViewModel`, with `Items`, `TotalCount`, `MaxResultCount` and `CurrentPage`. Each item has the shape of `Model.Content`, with only the fields the content type marks `ShowInList`:

````cshtml
@model ContentListRenderViewModel
<ul>
@foreach (var item in Model.Items)
{
    <li><a href="@item.Content.Url">@item.GetText("title")</a></li>
}
</ul>
````

`item.Content.Url` is the content's address, relative and with its language prefix. Filter values that are not valid, such as an unknown field or a month without a year, are ignored rather than failing the page.

### Other Queries

Inject the public application services for anything else a template needs:

* `IContentPublicAppService.GetAdjacentAsync` returns the previous and next contents of the same page and language, by publish time, for "older" and "newer" links.
* `IContentPublicAppService.GetTranslationsAsync` returns the language versions of a content.
* `IPagePublicAppService.FindByNameAsync` and `GetListAsync` read pages, for a navigation menu.
* `SelectFieldOptionsProvider` returns the options of a `Select` field, for a category menu; see [Select Options](template-localization.md#select-options).

## The Head

`site-head-metadata` writes the tags that come from the page: the description, the canonical URL, the robots tag, Open Graph, the Twitter card, the hreflang alternates and `x-default`. The `<title>`, the charset and the viewport stay the template's own:

````cshtml
<head>
    <meta charset="utf-8" />
    <title>@ViewBag.Title</title>
    <site-head-metadata metadata="@Model.HeadMetadata" title="@ViewBag.Title" current-page="@currentPage" />
</head>
````

| Attribute | Description |
|---|---|
| `metadata` | `Model.HeadMetadata`. |
| `title` | The title for `og:title`, when the template composes its own, such as a localized archive title. Defaults to `HeadMetadata.MetaTitle`. |
| `fallback-description` | A description to use when the content has none. |
| `current-page`, `page-query-key` | The page of a paged list, and its query parameter, `page` by default. Beyond the first page, the canonical URL and the alternates carry the page number. |

See [SEO](../seo/index.md) for what the metadata contains and where it comes from.

## Rendering from an External Front End

A front end in another process or another stack, such as Next.js or Astro, reads the same data from the public HTTP API that `Dignite.Site.Public.Web` uses:

| Endpoint | Returns |
|---|---|
| `GET /api/site-public/resolve-path?path=...` | What a path resolves to: the page, the content and its content type, the filter values and the language. |
| `GET /api/site-public/head-metadata?path=...` | The `<head>` metadata of a path. |
| `GET /api/site-public/contents` | Published contents, filtered and paged. |
| `GET /api/site-public/files/...` | The site's [files](../files.md#serving-files). |

The sitemap, the feeds and `robots.txt` are then the front end's to serve as well; their documents are available at `/api/site-public/sitemap`, `/api/site-public/feed` and `/api/site-public/robots`.

## See Also

* [Multilingual Sites](languages.md)
* [Localizing Template Texts](template-localization.md)
* [SEO](../seo/index.md)
