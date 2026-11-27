```json
//[doc-seo]
{
    "Description": "The robots.txt of a Dignite Site, with separate switches for search engines, AI training crawlers and AI search crawlers, how a site closed to indexing stays out of search results, and the llms.txt index of its pages."
}
```

# robots.txt and llms.txt

Each site serves its own `/robots.txt`, composed from its own settings, and can serve an `/llms.txt` index of its pages. Neither is edited by hand.

## robots.txt

Three [settings](../settings.md) decide what `robots.txt` allows:

| Setting | Default | Crawlers |
|---|---|---|
| `Site.Robots.AllowIndexing` | `true` | Every crawler, `User-agent: *` |
| `Site.Robots.AllowAiTraining` | `false` | Crawlers that collect training data for AI models: `GPTBot`, `Google-Extended`, `ClaudeBot`, `CCBot` |
| `Site.Robots.AllowAiSearch` | `true` | Crawlers that answer questions in AI search: `OAI-SearchBot`, `PerplexityBot`, `Claude-SearchBot` |

The defaults are the policy most site owners want: found by search engines and cited in AI answers, but not used to train models. With them, `robots.txt` reads:

````text
User-agent: *
Allow: /

# AI training crawlers
User-agent: GPTBot
Disallow: /
User-agent: Google-Extended
Disallow: /
User-agent: ClaudeBot
Disallow: /
User-agent: CCBot
Disallow: /

# AI search and RAG crawlers
User-agent: OAI-SearchBot
Allow: /
User-agent: PerplexityBot
Allow: /
User-agent: Claude-SearchBot
Allow: /

Sitemap: https://www.example.com/sitemap.xml
````

Each named crawler gets a group of its own, because a crawler obeys only the most specific group that matches it. For the same reason, turning `Site.Robots.AllowIndexing` off disallows every AI crawler's group too; see [A Site Closed to Indexing](#a-site-closed-to-indexing).

`Google-Extended` is not a crawler but Google's opt-out token for AI training; denying it does not affect Google Search.

The `Sitemap` URL is built on the site's [primary domain](../settings.md#primary-domain). `robots.txt` asks crawlers; it does not stop a crawler that ignores it.

## A Site Closed to Indexing

With `Site.Robots.AllowIndexing` set to `false`, the site asks search engines to leave it out of their results:

* every page has a `noindex` robots tag in its [head metadata](index.md#head-metadata);
* the pages, the sitemap, the feeds and `llms.txt` are sent with `X-Robots-Tag: noindex`, whatever the template writes in the head;
* `robots.txt` lets `Googlebot` and `Bingbot` fetch the pages, and disallows every other crawler.

A search engine never fetches a page that `robots.txt` disallows, so it never reads the page's `noindex`, and it can still list the address, without a description, when other sites link to it. Google and Bing obey `noindex`, so they are let in to read it; other crawlers, Baidu's and the AI crawlers among them, stay disallowed. Neither of the two may fetch the theme's static files or the site's files, which carry no `noindex` of their own:

````text
User-agent: *
Disallow: /

# Search engines that read noindex
User-agent: Googlebot
Allow: /
Disallow: /site-assets/
Disallow: /blobs/
User-agent: Bingbot
Allow: /
Disallow: /site-assets/
Disallow: /blobs/

# AI training crawlers
User-agent: GPTBot
Disallow: /
User-agent: Google-Extended
Disallow: /
User-agent: ClaudeBot
Disallow: /
User-agent: CCBot
Disallow: /

# AI search and RAG crawlers
User-agent: OAI-SearchBot
Disallow: /
User-agent: PerplexityBot
Disallow: /
User-agent: Claude-SearchBot
Disallow: /
````

There is no `Sitemap` line. The sitemap, the feeds and `llms.txt` are still served to whoever asks for them.

A page that a search engine has already indexed leaves its results once the engine crawls it again and reads `noindex`, which can take months for a page it rarely visits. Turning the setting back on works the same way: the pages are indexed as they are crawled again.

## llms.txt

`/llms.txt` serves a Markdown index of the site's active pages, for AI clients that look for one:

````text
# Example Company

## Pages
- [Example Company](https://www.example.com/)
- [About](https://www.example.com/about)
- [Blog](https://www.example.com/blog)
````

The heading is the display name of the home page. The links are the pages' own addresses in the default language, on the primary domain; contents are not listed. Every site serves it; there is no setting to turn it off. It lists only pages that the sitemap already lists, and a [site closed to indexing](#a-site-closed-to-indexing) sends it with `X-Robots-Tag: noindex`.

`llms.txt` is a convenience for AI clients, not a ranking signal: few sites serve one, and Google has said it does not use it.

## External Front Ends

`GET /api/site-public/robots` and `GET /api/site-public/robots/llms-txt` return both documents to a front end that serves the site from another process. Such a front end gets the `noindex` of a site closed to indexing in the head metadata, and sends `X-Robots-Tag` itself.

## See Also

* [SEO](index.md)
* [Sitemap and Feeds](sitemap-and-feeds.md)
* [Settings, Features and Permissions](../settings.md)
