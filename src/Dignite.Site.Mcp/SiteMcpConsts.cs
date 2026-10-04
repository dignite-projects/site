namespace Dignite.Site.Mcp;

public static class SiteMcpConsts
{
    /// <summary>
    /// The MCP namespace this module owns on the application's server: every tool is named
    /// <c>site_…</c> and every resource lives under <c>site://</c>.
    /// </summary>
    public const string ModuleName = "site";

    /// <summary>
    /// This module's section of the instructions handed to a client on connect - the one place to tell a
    /// model how these tools want to be used, before it has called anything.
    /// <para>
    /// It earns its length: the tool surface is cut by entity and addressed by name (总体设计 §6.2), so a
    /// client that does not first read the schema will guess at page and content type names that are the
    /// tenant's runtime data. Saying so here costs one message and saves a round trip of wrong guesses.
    /// It describes only the <c>site_*</c> tools - other modules on the same server add their own sections.
    /// </para>
    /// </summary>
    public const string Instructions =
        "Website content (site_* tools): these tools manage one website's content.\n\n" +
        "Start by reading the `site://schema` resource, or calling `site_get_schema` if your client " +
        "does not support resources. It returns the site's languages, its pages, the content types " +
        "under each page, and the fields of each content type - all addressed by name. Every other " +
        "site_* tool takes those names; none of them takes an id.\n\n" +
        "Writing content: pick the content type whose description matches what the user asked for " +
        "(\"post a news item\" is a content type named by the tenant, not by this server), then supply a " +
        "value for every field the schema marks required, keyed by the field's `name`.\n\n" +
        "Two things that are easy to get wrong:\n" +
        "- `slug` is always required. Pass an empty string only when the content IS the page (a home or " +
        "\"about\" page whose URL is the page's route). Otherwise pass a real slug.\n" +
        "- `cultureName` must be one of the schema's `enabledLanguages`. Do not invent a variant.";
}
