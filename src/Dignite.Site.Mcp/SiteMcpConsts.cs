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
        "- `cultureName` must be one of the schema's `enabledLanguages`. Do not invent a variant.\n\n" +
        "Theme (site_*theme*, site_*release* and site_preview tools): the site's look is a theme of files. A " +
        "file without an extension (`Events`, `blog/_sidebar`, `layouts/default`) is a Scriban template; one " +
        "with an extension (`css/site.css`) is a static file served as it is. A page renders through the " +
        "template whose path equals its `template` value, and through the Razor view of that name when the " +
        "theme has none. Saving a file changes only the working copy - the site does not change until " +
        "`site_publish_theme` publishes the whole working copy as a release. Before writing a template, read the " +
        "`site://theme-guide` resource (or call `site_get_theme_guide`): a template sees only the variables and " +
        "functions listed there, so what you remember of Liquid or Handlebars does not carry over. Work in this " +
        "order: `site_save_theme_file`, then `site_check_theme` and fix every error it lists, then `site_preview` on a " +
        "path and read the HTML, then `site_publish_theme` with the check's fingerprint. To undo a release, " +
        "`site_set_live_release` with an older one. A page template picks its layout in front matter - a first " +
        "line `+++`, then `layout = \"layouts/landing\"` (or `\"none\"`), then `+++` - else it uses the theme's " +
        "default layout. Every reference is a full path from the theme's root: `{{ include \"blog/_sidebar\" }}`, " +
        "`{{ asset_url \"css/site.css\" }}`. Binary files (images, fonts) are not theme files: upload them with " +
        "site_create_upload_url (to the theme-assets container) and site_confirm_file.";

    /// <summary>
    /// How Site's fields use files, and the tools that keep them (<c>FileTools</c>). Separate from
    /// <see cref="Instructions"/> so each stays about one surface; both are handed to the client.
    /// </summary>
    public const string FileInstructions =
        "Site files: upload images and attachments by link - `site_list_file_containers` first, for the containers " +
        "you may upload to and their limits; then `site_create_upload_url` for a container, run the command it returns " +
        "on the machine the file is on, and `site_confirm_file` with the fileId to keep it. Site uses three containers: " +
        "`images` for pictures a content field shows (JPEG, PNG, GIF, WebP; no SVG), `attachments` for documents to " +
        "download (PDF, Office, text, CSV, ZIP; no pictures), and `theme-assets` for the binary files a theme links " +
        "(favicon, web fonts). A file field's configuration names one of them.\n\n" +
        "What to store: a file field takes an array of the file objects `site_confirm_file`, `site_get_file` and " +
        "`site_list_files` return (keep `id`, `name` and `url`); an SEO share image, the default share image and a " +
        "template take the file's `url`. Keep the `id` too: deleting a file (`site_delete_file`) needs it, and " +
        "`site_list_files` finds it again by name.";
}
