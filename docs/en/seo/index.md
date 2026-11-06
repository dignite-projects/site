```json
//[doc-seo]
{
    "Description": "SEO in Dignite Site: the head metadata of every page and content, canonical URLs on the primary domain, hreflang alternates, noindex, Open Graph images and filtered views."
}
```

# SEO

Site derives what search engines need from the site's own data, so it never drifts from what the site serves:

* The **head metadata** of every page and content: title, description, canonical URL, robots, Open Graph, Twitter card and hreflang alternates, the page's **breadcrumb**, and a safe way to write the **structured data** a template chooses.
* A **[sitemap](sitemap-and-feeds.md)** of every page and published content, and **[feeds](sitemap-and-feeds.md#feeds)** of each page.
* **[robots.txt](robots-and-llms-txt.md)** with switches for search, AI training and AI search crawlers, and an optional **[llms.txt](robots-and-llms-txt.md)**.

All of them follow four rules:

* **Absolute URLs are built on the site's primary domain**, the `Site.PrimaryDomain` [setting](../settings.md#primary-domain), never on the host a request arrived on, so a custom domain and a platform subdomain never compete as duplicates.
* **The canonical URL refers to itself**, and is derived from the route; it is not something an author sets.
* **Every language has its own URL**, and its translations are linked by hreflang.
* **Only what visitors can see is advertised**: drafts, scheduled and archived contents, inactive pages and contents marked `noindex` are left out.

## Head Metadata

A template writes the head metadata with `site_head` in a [Scriban template](../rendering/scriban-templates.md#the-head), or with the `site-head-metadata` tag helper in a [Razor template](../rendering/razor-templates.md#the-head). For a request, it holds:

| Tag | Value |
|---|---|
| `<title>`, `og:title` | The SEO field's `metaTitle`, or the first text field of the content that has a value, or the slug. |
| `description`, `og:description` | The SEO field's `metaDescription`. Without one there is no tag. |
| `canonical`, `og:url` | The absolute URL of the content or the page, in its language. |
| `robots` | `noindex`, when the content is marked so or the request is a [truncated filtered view](#filtered-views); otherwise no tag. |
| `og:type` | `website`, or `article` when the template says so, with `article:published_time` and `article:modified_time` from the content. |
| `og:site_name` | The `Site.Branding.AppName` [setting](../settings.md); without one there is no tag. |
| `og:locale`, `og:locale:alternate` | The page's language and the languages of its translations, as Open Graph writes them: `ja` is `ja_JP`, `en` is `en_US`, `zh-Hans` is `zh_CN`, `zh-Hant` is `zh_TW`, and a language that names its territory keeps it (`en-GB` is `en_GB`). |
| `og:image`, `og:image:width`, `og:image:height` | See [Share Images](#share-images). |
| `twitter:card` | `summary_large_image` with an image, `summary` without. |
| `hreflang` alternates, `x-default` | See [Hreflang Alternates](#hreflang-alternates). |

The title and the description of a content come from the [SEO field](../field-types.md#seo) when its content type includes one. A content type without it still gets a title, from its first text field that has a value, but no description: no other field is taken for one, since a year, a price or a category is not what the page is about. Add the SEO field to the content types whose pages should have a description. A request for a page itself takes them from the page's own content, the one with an empty slug, when it has one, and from the page's display name otherwise.

`HeadMetadata` is also in the template's view model, and `/api/site-public/head-metadata?path=...` returns it to an external front end.

### Share Images

The share image of a page is the first of:

1. the `ogImage` of the content's SEO field, which its author chose;
2. the image the template gives `site_head` (or `site-head-metadata`), such as a post's cover;
3. the site's default share image, the `Site.Seo.DefaultShareImage` [setting](../settings.md).

Without any, there is no `og:image` tag; Site does not guess one from other fields. An image from the site's [file library](../files.md) is requested at 1200 x 630, cropped to fill, made absolute on the primary domain, and written with `og:image:width` and `og:image:height`. An image at any other absolute URL is used as it is, without a size. `og:image:alt` is left to the template.

### Title and Description Length

A search result shows about 60 columns of a title and 160 of a description, where a Chinese, Japanese or Korean character takes two columns. The SEO field's limits are in columns, and they are advice, not rules: the admin UI counts the columns beside each field, and the MCP tools that store a content say in `warnings` when a title or a description is wider. Nothing is cut when the page is rendered: a search engine rewrites a snippet as it sees fit, and a cut would add an ellipsis the page does not have.

### noindex

Setting `noIndex` in a content's SEO field:

* writes a `noindex` robots tag on the content;
* removes the content from the sitemap and the feeds;
* removes it from the hreflang alternates of its translations.

For a page's own content, the page's address goes with it, so an "about" page marked `noindex` leaves the sitemap entirely.

## Hreflang Alternates

The translations of a content are the contents of the same page and content type with the same slug in other languages; see [Languages and Translations](../contents.md#languages-and-translations). Its hreflang alternates list every published translation in a language the site serves, the content itself included, so the set always refers to itself.

A page itself, such as a list, has an alternate in the default language and in every other language with a published content beneath the page.

`x-default` points at the default language's version, the one with no language prefix, when the set has one and more than one language.

## Filtered Views

A request for a page with [filter values](../pages-and-routing.md#resolving-a-request) is one of two kinds:

* **A declared view** is an address the route spells out, such as `/news/tutorials` for `/news/{category?:^(news|tutorials)$}`. It is a real landing page: canonical at itself, indexable, with alternates at the same filtered path in other languages. Its title is the page's title followed by the filter values, such as "News - tutorials" or "News - 2026 / 08"; a template can compose a localized one, as [Category and Month Archive Titles](../rendering/template-localization.md#category-and-month-archive-titles) shows.
* **A truncated view** is a deeper route read with its last placeholders left out, such as `/blog/2026-08` for `/blog/{publishTime:yyyy-MM}/{slug}`. No route declares that address, so it is `noindex`, with the canonical URL of the unfiltered page.

A declared view whose date values do not form a whole period, such as a month without a year, lists the unfiltered page and is treated as a truncated view.

## Paged Lists

Beyond the first page of a list, the canonical URL and every alternate carry the page number, `?page=2`, since page 2 lists different contents than page 1. Pass the page number to `site-head-metadata` with `current-page`.

## Breadcrumbs

Every request carries its breadcrumb, derived from the page tree like the canonical URL: the home page, the page's ancestors by their parent page, the page, and the content when the request names one. Each step is named by its title in the language being viewed: a page by its own content's title, or its display name without one. An inactive ancestor is left out, and a page's own content (the one with an empty slug) is the page's step, not one more. Each step has a site-relative `path`, for a link on the page, an absolute `url`, for structured data, and the `page_name` of its page, with the content's `slug` on a content's step, so that a template can label a step with a short text of its own instead of the title. A Scriban template reads it as `breadcrumb`, a Razor template as `Model.Breadcrumb`, and an external front end in the head metadata.

## Structured Data

Which schema.org types a page carries, and what goes in them, depends on what each site publishes, so the template decides it, and gives the nodes to `site_head` as `schema` (or to `site-head-metadata`). Site writes them correctly: in one `@graph`, without empty properties, with `~/` and file library addresses made absolute on the primary domain, and with `<`, `>` and `&` escaped so that no text can end the script. See [Structured Data](../rendering/scriban-templates.md#structured-data).

Google still shows rich results for `Organization`, `WebSite`, `BreadcrumbList`, `Article` and `Product`; `FAQPage` and `HowTo` no longer earn one, though AI assistants read them.

## What Site Leaves to the Template

* `<title>` itself, charset and viewport: the template knows its branding.
* The shape of the structured data, as above.
* Statistics and site verification tags: written in the layout.
* Redirects of old URLs: Site keeps no redirect table, so a changed slug or route is a new address.

## See Also

* [Sitemap and Feeds](sitemap-and-feeds.md)
* [robots.txt and llms.txt](robots-and-llms-txt.md)
* [Field Types](../field-types.md#seo)
