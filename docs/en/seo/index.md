```json
//[doc-seo]
{
    "Description": "SEO in Dignite Site: the head metadata of every page and content, canonical URLs on the primary domain, hreflang alternates, noindex, Open Graph images and filtered views."
}
```

# SEO

Site derives what search engines need from the site's own data, so it never drifts from what the site serves:

* The **head metadata** of every page and content: title, description, canonical URL, robots, Open Graph, Twitter card and hreflang alternates.
* A **[sitemap](sitemap-and-feeds.md)** of every page and published content, and **[feeds](sitemap-and-feeds.md#feeds)** of each page.
* **[robots.txt](robots-and-llms-txt.md)** with switches for search, AI training and AI search crawlers, and an optional **[llms.txt](robots-and-llms-txt.md)**.

All of them follow four rules:

* **Absolute URLs are built on the site's primary domain**, the `Site.PrimaryDomain` [setting](../settings.md#primary-domain), never on the host a request arrived on, so a custom domain and a platform subdomain never compete as duplicates.
* **The canonical URL refers to itself**, and is derived from the route; it is not something an author sets.
* **Every language has its own URL**, and its translations are linked by hreflang.
* **Only what visitors can see is advertised**: drafts, scheduled and archived contents, inactive pages and contents marked `noindex` are left out.

## Head Metadata

A template writes the head metadata with the `site-head-metadata` tag helper; see [The Head](../rendering/templates.md#the-head). For a request, it holds:

| Tag | Value |
|---|---|
| `<title>`, `og:title` | The SEO field's `metaTitle`, or the first text field of the content that has a value, or the slug. |
| `description`, `og:description` | The SEO field's `metaDescription`, or the next text field that has a value. |
| `canonical`, `og:url` | The absolute URL of the content or the page, in its language. |
| `robots` | `noindex`, when the content is marked so or the request is a [truncated filtered view](#filtered-views); otherwise no tag. |
| `og:image` | The SEO field's `ogImage`; see [Share Images](#share-images). |
| `twitter:card` | `summary_large_image` with an image, `summary` without. |
| `hreflang` alternates, `x-default` | See [Hreflang Alternates](#hreflang-alternates). |

The title and the description of a content come from the [SEO field](../field-types.md#seo) when its content type includes one. A content type without it still gets a title and a description, from its first text fields. A request for a page itself takes them from the page's own content, the one with an empty slug, when it has one, and from the page's display name otherwise.

`HeadMetadata` is also in the template's view model, and `/api/site-public/head-metadata?path=...` returns it to an external front end.

### Share Images

An `og:image` from the site's [file library](../files.md) is requested at 1200 x 630, cropped to fill, and made absolute on the primary domain. An image at any other absolute URL is used as it is. Without a share image, there is no `og:image` tag; Site does not guess one from other fields.

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

## What Site Leaves to the Template

* `<title>` itself, charset and viewport: the template knows its branding.
* JSON-LD structured data: its vocabulary depends on what each site publishes, so the template writes it.
* Redirects of old URLs: Site keeps no redirect table, so a changed slug or route is a new address.

## See Also

* [Sitemap and Feeds](sitemap-and-feeds.md)
* [robots.txt and llms.txt](robots-and-llms-txt.md)
* [Field Types](../field-types.md#seo)
