using System.Collections.Generic;

namespace Dignite.Site.Host.Data;

/// <summary>
/// The Scriban theme that <see cref="SiteThemeDataSeedContributor"/> publishes next to the demo site of
/// <see cref="SiteContentDataSeedContributor"/>. It is a fixture for trying the theme features by hand, so each
/// template leans on a different part of what a template can use:
/// <list type="bullet">
/// <item><c>home</c>: <c>contents</c> and <c>content_counts</c> of other pages, by name.</item>
/// <item><c>news</c>: the route's field placeholders (<c>field_filters</c>, <c>published_after</c>), the
/// counts per category on the current page, paging, and the labels of a <c>Select</c> field read from its
/// configuration with <c>find_page</c> and <c>field_definitions</c>.</item>
/// <item><c>events</c>: a plain paged list and a detail page.</item>
/// <item><c>Default</c>: every field of a content with <c>render_field</c>; serves about, contact and the legal pages.</item>
/// </list>
/// Template paths are the pages' <c>Template</c> values, so the two must change together.
/// </summary>
internal static class SeedTheme
{
    public const string HomeTemplate = "home";
    public const string NewsTemplate = "news";
    public const string EventsTemplate = "events";
    public const string DefaultTemplate = "Default";

    public static IReadOnlyList<(string Path, string Body)> Files { get; } = new[]
    {
        ("layouts/default", Layout),
        ("css/site.css", Stylesheet),
        (HomeTemplate, Home),
        (NewsTemplate, News),
        (EventsTemplate, Events),
        (DefaultTemplate, Default),
        ("_news-card", NewsCard),
        ("_news-labels", NewsLabels),
        ("_category-nav", CategoryNav),
        ("_pager", Pager)
    };

    private const string Layout = """
        <!DOCTYPE html>
        <html lang="{{ culture }}">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>{{ head?.meta_title ?? page.display_name }}</title>
            {{ site_head }}
            <link rel="stylesheet" href="{{ asset_url "css/site.css" }}" />
        </head>
        <body>
        <header class="site-header">
            <a class="brand" href="{{ url "~/" }}">Dignite Demo</a>
            <nav>
                <a href="{{ url "~/" }}"{{ if page.is_home_page }} class="active"{{ end }}>Home</a>
                <a href="{{ url "~/about" }}"{{ if page.name == "about" }} class="active"{{ end }}>About</a>
                <a href="{{ url "~/news" }}"{{ if page.name == "news" }} class="active"{{ end }}>News</a>
                <a href="{{ url "~/events" }}"{{ if page.name == "events" }} class="active"{{ end }}>Events</a>
                <a href="{{ url "~/contact" }}"{{ if page.name == "contact" }} class="active"{{ end }}>Contact</a>
            </nav>
            <div class="languages">
                {{ for lang in languages }}<a href="{{ lang.url }}"{{ if lang.is_current }} class="active"{{ end }}{{ if !lang.is_translation }} title="Not translated yet"{{ end }}>{{ lang.culture_name }}</a>{{ end }}
            </div>
        </header>
        <main>
        {{ body }}
        </main>
        <footer class="site-footer">
            <a href="{{ url "~/privacy-policy" }}">Privacy</a>
            <a href="{{ url "~/terms-of-service" }}">Terms</a>
        </footer>
        </body>
        </html>
        """;

    private const string Stylesheet = """
        :root { --ink: #1f2933; --muted: #6b7785; --line: #e3e8ee; --accent: #2457d6; --soft: #f4f6fa; }
        * { box-sizing: border-box; }
        body { margin: 0; font: 16px/1.6 system-ui, sans-serif; color: var(--ink); }
        a { color: var(--accent); text-decoration: none; }
        a:hover { text-decoration: underline; }
        main, .site-header, .site-footer { max-width: 960px; margin: 0 auto; padding: 0 1rem; }
        main { padding-top: 1.5rem; padding-bottom: 3rem; }
        .site-header { display: flex; flex-wrap: wrap; align-items: center; gap: 1rem; padding-top: 1rem; padding-bottom: 1rem; border-bottom: 1px solid var(--line); }
        .site-header .brand { font-weight: 700; color: var(--ink); margin-right: auto; }
        .site-header nav a, .languages a { margin-right: .75rem; color: var(--muted); }
        .site-header a.active { color: var(--ink); font-weight: 600; }
        .site-footer { padding-top: 1rem; padding-bottom: 2rem; border-top: 1px solid var(--line); color: var(--muted); }
        .site-footer a { margin-right: 1rem; }
        .hero { background: var(--soft); border-radius: 8px; padding: 1.5rem; margin-bottom: 2rem; }
        .columns { display: grid; grid-template-columns: 14rem 1fr; gap: 2rem; }
        @media (max-width: 700px) { .columns { grid-template-columns: 1fr; } }
        .side h2 { font-size: 1rem; margin: 0 0 .5rem; }
        .side ul { list-style: none; margin: 0 0 1.5rem; padding: 0; }
        .side li { padding: .15rem 0; }
        .side li.active a { color: var(--ink); font-weight: 600; }
        .count { color: var(--muted); font-size: .85rem; }
        .card { padding: 1rem 0; border-bottom: 1px solid var(--line); }
        .card h3 { margin: .25rem 0; }
        .meta { color: var(--muted); font-size: .85rem; margin: 0; }
        .tag { display: inline-block; padding: 0 .5rem; border-radius: 999px; background: var(--soft); color: var(--ink); font-size: .8rem; }
        .badge { display: inline-block; padding: 0 .5rem; border-radius: 4px; background: #fff4d6; font-size: .8rem; }
        .pager { display: flex; gap: .5rem; margin-top: 1.5rem; }
        .pager a, .pager span { padding: .15rem .6rem; border: 1px solid var(--line); border-radius: 4px; }
        .pager span.current { background: var(--ink); color: #fff; border-color: var(--ink); }
        .months a { margin-right: .5rem; }
        .stats { display: flex; gap: 2rem; }
        """;

    // The labels of the categories come from the options of the Select field, not from the template, and the
    // result is read by the partials that follow it: whatever `_news-labels` assigns stays visible to them.
    private const string NewsLabels = """
        {{ category_labels = {} }}
        {{ news_page = find_page "news" }}
        {{ if news_page }}
            {{ news_fields = field_definitions news_page.content_types[0].fields }}
            {{ if news_fields.category }}
                {{ for option in news_fields.category.configuration["Select.Options"] }}
                    {{ category_labels[option.Value] = option.Text }}
                {{ end }}
            {{ end }}
        {{ end }}
        """;

    // `$1` is whether to count the page being rendered, with the route's filters applied, or all of the news.
    private const string CategoryNav = """
        {{ if $1 }}
            {{ groups = content_counts { field: "category" } }}
        {{ else }}
            {{ groups = content_counts { field: "category", page: "news" } }}
        {{ end }}
        {{ counts = {} }}
        {{ total = 0 }}
        {{ for group in groups }}
            {{ counts[group.value] = group.count }}
            {{ total = total + group.count }}
        {{ end }}
        <ul>
            <li{{ if !field_filters["category"] }} class="active"{{ end }}><a href="{{ url "~/news" }}">All</a> <span class="count">({{ total }})</span></li>
            {{ for name in (object.keys category_labels) }}
                <li{{ if field_filters["category"] == name }} class="active"{{ end }}>
                    <a href="{{ url ("~/news/" + name) }}">{{ category_labels[name] }}</a> <span class="count">({{ counts[name] ?? 0 }})</span>
                </li>
            {{ end }}
        </ul>
        """;

    private const string NewsCard = """
        <article class="card">
            <p class="meta">
                <span class="tag">{{ category_labels[item.fields.category] ?? item.fields.category }}</span>
                {{ item.publish_time | date.to_string "%Y-%m-%d" }}
                {{ if item.fields.featured }}<span class="badge">Featured</span>{{ end }}
            </p>
            <h3><a href="{{ item.url }}">{{ item.fields.title }}</a></h3>
            <p>{{ item.fields.summary }}</p>
        </article>
        """;

    // Expects `list`, the page of contents being shown, and `page_no`.
    private const string Pager = """
        {{ last = (list.total_count + list.max_result_count - 1) // list.max_result_count }}
        {{ if last > 1 }}
        <nav class="pager" aria-label="Pages">
            {{ if page_no > 1 }}<a href="{{ page_url (page_no - 1) }}" rel="prev">&laquo;</a>{{ end }}
            {{ for n in 1..last }}
                {{ if n == page_no }}<span class="current">{{ n }}</span>{{ else }}<a href="{{ page_url n }}">{{ n }}</a>{{ end }}
            {{ end }}
            {{ if page_no < last }}<a href="{{ page_url (page_no + 1) }}" rel="next">&raquo;</a>{{ end }}
        </nav>
        <p class="meta">{{ list.total_count }} items, {{ list.max_result_count }} per page</p>
        {{ end }}
        """;

    private const string Home = """
        {{ include "_news-labels" }}
        <section class="hero">
            {{ if content }}
                <h1>{{ content.fields.title }}</h1>
                {{ render_field "body" true }}
            {{ end }}
        </section>

        <div class="columns">
            <aside class="side">
                <h2>News by category</h2>
                {{ include "_category-nav" false }}
            </aside>
            <section>
                <h2>Latest news</h2>
                {{ latest = contents { page: "news", max: 3 } }}
                {{ for item in latest.items }}
                    {{ include "_news-card" }}
                {{ end }}
                <p><a href="{{ url "~/news" }}">All news</a></p>

                <h2>Events</h2>
                {{ upcoming = contents { page: "events", max: 3 } }}
                {{ for item in upcoming.items }}
                    <article class="card">
                        <p class="meta">{{ item.fields.event_date | date.parse | date.to_string "%Y-%m-%d" }} &middot; {{ item.fields.location }}</p>
                        <h3><a href="{{ item.url }}">{{ item.fields.title }}</a></h3>
                    </article>
                {{ end }}
                <p><a href="{{ url "~/events" }}">All events ({{ upcoming.total_count }})</a></p>
            </section>
        </div>
        """;

    private const string News = """
        {{ include "_news-labels" }}
        {{ if content }}
            <article>
                <p class="meta">
                    <a href="{{ url "~/news" }}">News</a> /
                    <a href="{{ url ("~/news/" + content.fields.category) }}">{{ category_labels[content.fields.category] ?? content.fields.category }}</a>
                </p>
                <h1>{{ content.fields.title }}</h1>
                <p class="meta">
                    {{ content.publish_time | date.to_string "%Y-%m-%d" }}
                    {{ if content.fields.featured }}<span class="badge">Featured</span>{{ end }}
                </p>
                <p><strong>{{ content.fields.summary }}</strong></p>
                {{ render_field "body" true }}
            </article>
        {{ else }}
            <h1>
                News
                {{ if field_filters["category"] }}/ {{ category_labels[field_filters["category"]] }}{{ end }}
                {{ if published_after }}/ {{ published_after | date.to_string "%Y-%m" }}{{ end }}
            </h1>
            <div class="columns">
                <aside class="side">
                    <h2>Categories</h2>
                    {{ include "_category-nav" true }}
                    {{ if field_filters["category"] }}
                        <h2>Months</h2>
                        <p class="months">
                            {{ for month in ["2026-06", "2026-07", "2026-08", "2026-09", "2026-10"] }}
                                <a href="{{ url ("~/news/" + field_filters["category"] + "/" + month) }}">{{ month }}</a>
                            {{ end }}
                        </p>
                    {{ end }}
                </aside>
                <section>
                    {{ list = contents { max: 10, page_no: page_no } }}
                    {{ for item in list.items }}
                        {{ include "_news-card" }}
                    {{ else }}
                        <p>Nothing here.</p>
                    {{ end }}
                    {{ include "_pager" }}
                </section>
            </div>
        {{ end }}
        """;

    private const string Events = """
        {{ if content }}
            <article>
                <p class="meta"><a href="{{ url "~/events" }}">Events</a></p>
                <h1>{{ content.fields.title }}</h1>
                <p class="meta">{{ content.fields.event_date | date.parse | date.to_string "%Y-%m-%d" }} &middot; {{ content.fields.location }}</p>
                {{ render_field "body" true }}
            </article>
        {{ else }}
            <h1>Events</h1>
            <p>Talks and meetups from the Dignite team.</p>
            {{ list = contents { max: 10, page_no: page_no } }}
            {{ for item in list.items }}
                <article class="card">
                    <p class="meta">{{ item.fields.event_date | date.parse | date.to_string "%Y-%m-%d" }} &middot; {{ item.fields.location }}</p>
                    <h3><a href="{{ item.url }}">{{ item.fields.title }}</a></h3>
                </article>
            {{ end }}
            {{ include "_pager" }}
        {{ end }}
        """;

    private const string Default = """
        {{ if content }}
            <h1>{{ page.display_name }}</h1>
            {{ for field in field_list }}
                <section>
                    {{ render_field field.name }}
                </section>
            {{ end }}
        {{ else }}
            <h1>{{ page.display_name }}</h1>
            {{ list = contents { max: 20, page_no: page_no } }}
            <ul>
                {{ for item in list.items }}<li><a href="{{ item.url }}">{{ item.slug }}</a></li>{{ end }}
            </ul>
            {{ include "_pager" }}
        {{ end }}
        """;
}
