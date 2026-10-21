```json
//[doc-seo]
{
    "Description": "The sitemap and feeds of a Dignite Site: a sharded sitemap of every page and published content with honest lastmod dates, and RSS, Atom and JSON feeds for every page in every language."
}
```

# Sitemap and Feeds

`Dignite.Site.Public.Web` serves a site's sitemap and feeds on the site's own domain. Both are built from the same data, the pages and their published contents, with the same exclusions, so they never advertise different sets of URLs.

## Sitemap

`/sitemap.xml` is a sitemap index. It lists the shards, `/sitemap-1.xml`, `/sitemap-2.xml` and so on, each with up to 10,000 URLs. The URLs of one page are kept together, page addresses before the contents beneath them.

The sitemap lists:

* The own address of every active page, in the default language, and in every other language that has a published content beneath the page.
* Every published content whose publish time has passed, at its URL in its language.

It leaves out drafts, archived and scheduled contents, inactive pages, contents in a language the site no longer serves, and contents marked `noindex` in their [SEO field](../field-types.md#seo), together with the page address of a page's own content marked so.

| Element | Value |
|---|---|
| `loc` | The absolute URL, on the site's [primary domain](../settings.md#primary-domain). |
| `lastmod` | When the content last changed: the later of its last modification and its publish time. A page's is the newest of the contents beneath it. |
| `priority` | `1.0` for the home page, `0.8` for other pages and `0.5` for contents. |

`lastmod` is computed only from stored dates, never from when the sitemap was generated: a sitemap whose every URL changed today teaches search engines to ignore `lastmod` for the whole site. `changefreq` is left out; crawlers ignore it.

`robots.txt` names the sitemap index while indexing is allowed; see [robots.txt and llms.txt](robots-and-llms-txt.md).

## Feeds

Every active page has a feed in every language the site serves, in three formats. Append the format's file name to the page's own address:

| Format | Address | Content type |
|---|---|---|
| RSS 2.0 | `/blog/feed.xml` | `application/rss+xml` |
| Atom 1.0 | `/blog/atom.xml` | `application/atom+xml` |
| JSON Feed 1.1 | `/blog/feed.json` | `application/feed+json` |

A language other than the default one keeps its prefix, `/ja/blog/feed.xml`, and the home page's feeds are at `/feed.xml`, `/atom.xml` and `/feed.json`.

A feed lists the 20 newest published contents beneath the page in that language, newest first, with the same exclusions as the sitemap. Each item has the content's title and summary, resolved as for the [head metadata](index.md#head-metadata): the SEO field's `metaTitle` and `metaDescription`, or the content's first text fields. A summary taken from a field is cut at 500 characters; a `metaDescription` is used whole.

Site does not write the `<link rel="alternate">` tag that lets browsers and feed readers discover a feed. Add it to the template of the pages that have one. `Url.SiteContent` gives the page address its language prefix; it leaves a path ending in a file name alone, so append the file name after it:

````cshtml
<link rel="alternate" type="application/rss+xml" title="@Model.Page.DisplayName"
      href="@Url.SiteContent("~/blog")/feed.xml" />
````

## External Front Ends

A front end that renders the site in another process serves these documents itself, from the public API:

| Endpoint | Returns |
|---|---|
| `GET /api/site-public/sitemap` | The sitemap index. |
| `GET /api/site-public/sitemap/shard?shardNumber=1` | One shard. |
| `GET /api/site-public/feed/resolve?feedPath=ja/blog/feed.xml` | The feed a path names. |

## See Also

* [SEO](index.md)
* [robots.txt and llms.txt](robots-and-llms-txt.md)
* [Multilingual Sites](../rendering/languages.md)
