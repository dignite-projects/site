namespace Dignite.Sites.Settings;

public static class SitesSettings
{
    public const string GroupName = "Sites";

    /// <summary>
    /// Comma-separated culture names this site serves. The <b>first entry is the default language</b>:
    /// it is the one whose URLs carry no culture prefix, and there is deliberately no second
    /// "default language" setting that could contradict this list (总体设计 §5.5).
    /// </summary>
    public const string EnabledLanguages = GroupName + ".EnabledLanguages";

    /// <summary>
    /// The site's primary absolute origin, e.g. <c>https://acme.com</c>, optionally with a base path.
    /// <para>
    /// This is the <i>forward</i> half of §4.2 - "what is this tenant's domain" - which is an ordinary
    /// tenant setting. The reverse half, "given this Host header, which tenant", is a lookup that
    /// settings cannot express and is out of scope for this module (GitHub issue #12): resolving a host
    /// to a tenant is a deploying host's <c>AbpTenantResolveOptions</c> configuration.
    /// </para>
    /// <para>
    /// Every generated document - sitemap, robots, feeds, and later canonical and hreflang - builds its
    /// absolute URLs from this one value rather than from whatever host a request happened to arrive on,
    /// which is what keeps a custom domain and a platform subdomain from becoming duplicate content (§5).
    /// Left blank, the request's own scheme and host are used instead.
    /// </para>
    /// </summary>
    public const string PrimaryDomain = GroupName + ".PrimaryDomain";

    /// <summary>
    /// Whether <c>/llms.txt</c> is served. Off by default deliberately - see <c>LlmsTxtBuilder</c>.
    /// </summary>
    public const string EnableLlmsTxt = GroupName + ".EnableLlmsTxt";

    public static class Robots
    {
        public const string AllowIndexing = GroupName + ".Robots.AllowIndexing";
        public const string AllowAiTraining = GroupName + ".Robots.AllowAiTraining";
        public const string AllowAiSearch = GroupName + ".Robots.AllowAiSearch";
    }
}
