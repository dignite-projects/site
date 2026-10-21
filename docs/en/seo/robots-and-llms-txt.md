```json
//[doc-seo]
{
    "Description": "The robots.txt of a Dignite Site, with separate switches for search engines, AI training crawlers and AI search crawlers, and the optional llms.txt index of its pages."
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

Each named crawler gets a group of its own, because a crawler obeys only the most specific group that matches it. For the same reason, turning `Site.Robots.AllowIndexing` off disallows every group, the AI crawlers included, and drops the `Sitemap` line: a site closed to search engines is closed to all of them.

`Google-Extended` is not a crawler but Google's opt-out token for AI training; denying it does not affect Google Search.

The `Sitemap` URL is built on the site's [primary domain](../settings.md#primary-domain). `robots.txt` asks crawlers; it does not stop a crawler that ignores it.

## llms.txt

With `Site.EnableLlmsTxt` set to `true`, `/llms.txt` serves a Markdown index of the site's active pages, for AI clients that look for one:

````text
# Example Company

## Pages
- [Example Company](https://www.example.com/)
- [About](https://www.example.com/about)
- [Blog](https://www.example.com/blog)
````

The heading is the display name of the home page. The links are the pages' own addresses in the default language, on the primary domain; contents are not listed. While the setting is off, the default, `/llms.txt` answers `404`.

`llms.txt` is an optional convenience, not a ranking signal: few sites serve one, and Google has said it does not use it.

## External Front Ends

`GET /api/site-public/robots` and `GET /api/site-public/robots/llms-txt` return both documents to a front end that serves the site from another process.

## See Also

* [SEO](index.md)
* [Sitemap and Feeds](sitemap-and-feeds.md)
* [Settings, Features and Permissions](../settings.md)
