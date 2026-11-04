using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Admin.Themes;
using Dignite.Site.Mcp.Errors;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Themes;

/// <summary>
/// The site's theme over MCP: a working copy of files, published as a whole into numbered releases. Files are
/// addressed by path and releases by number, so no tool takes an id.
/// <para>
/// The theme is not part of <c>site_get_schema</c> / <c>site://schema</c>: it is rendering, not site structure, and a
/// model that is not touching rendering should not pay for it in every schema read. <c>site_list_theme_files</c> is how
/// its files are found.
/// </para>
/// </summary>
[McpServerToolType]
public class SiteThemeTools : ITransientDependency
{
    /// <summary>
    /// The most HTML <c>site_preview</c> returns, in characters: enough to check a page - its head, its markup, the
    /// content it shows - and small enough to sit in a model's context next to everything else it is working on. A
    /// longer page is cut off and says so.
    /// </summary>
    public const int MaxPreviewHtmlLength = 32 * 1024;

    /// <summary>
    /// <see cref="MaxPreviewHtmlLength"/> as text, for the tool description - an attribute argument must be a constant,
    /// and a number cannot be turned into a constant string. Keep the two equal.
    /// </summary>
    private const string MaxPreviewHtmlLengthText = "32768";

    protected ISiteThemeAdminAppService ThemeAppService { get; }

    public SiteThemeTools(ISiteThemeAdminAppService themeAppService)
    {
        ThemeAppService = themeAppService;
    }

    [McpServerTool(Name = "site_get_theme_guide", Title = "Get the theme guide", ReadOnly = true)]
    [Description(
        "Returns how to write this site's theme, as Markdown: the variables a template sees (page, content, " +
        "content.fields, languages, head, body ...), every function it can call (site_head, url, asset_url, " +
        "file_url, contents, render_field, include ...), layouts and front matter, partials, paging, static " +
        "files and the limits of the sandbox. A template reaches nothing else, so read this before writing one " +
        "- a template written from what Liquid or Handlebars would allow fails the check. The same document as " +
        "the site://theme-guide resource.")]
    [Authorize(SiteAdminPermissions.Themes.Default)]
    public virtual Task<string> GetThemeGuideAsync()
    {
        return Task.FromResult(SiteThemeGuide.Text);
    }

    [McpServerTool(Name = "site_list_theme_files", Title = "List theme files", ReadOnly = true)]
    [Description(
        "Lists the files of the site's theme (its working copy), with how each stands against the live release: " +
        "unchanged, modified, added, or deleted (live, but removed from the working copy). A path without an " +
        "extension is a Scriban template; one with an extension is a static file (CSS, JS, ...) served at " +
        "/site-assets/<path>. Also returns the theme's default layout and the live release's number.")]
    [Authorize(SiteAdminPermissions.Themes.Default)]
    public virtual async Task<SiteThemeOverview> ListThemeFilesAsync()
    {
        var theme = await ThemeAppService.GetAsync();
        var files = await ThemeAppService.GetFilesAsync();

        return new SiteThemeOverview
        {
            DefaultLayout = theme.DefaultLayout,
            LiveReleaseNumber = theme.LiveReleaseNumber,
            Files = files.Items
                .Select(file => new SiteThemeFileEntry
                {
                    Path = file.Path,
                    IsAsset = file.IsAsset,
                    Status = file.Status.ToString(),
                    LastModificationTime = file.LastModificationTime
                })
                .ToArray()
        };
    }

    [McpServerTool(Name = "site_get_theme_file", Title = "Get a theme file", ReadOnly = true)]
    [Description(
        "Reads one file of the theme's working copy, with its syntax errors if it is a template that does not parse. " +
        "Pass 'release' to read the file as a release has it instead - for instance before rolling back. Read the " +
        "current body before changing a file: site_save_theme_file replaces it with exactly what you send.")]
    [Authorize(SiteAdminPermissions.Themes.Default)]
    public virtual async Task<SiteThemeFileContent> GetThemeFileAsync(
        [Description("The file's path, e.g. 'layouts/default', 'Events' or 'css/site.css', from site_list_theme_files.")]
        string path,
        [Description("A release number from site_list_releases. Omit for the working copy.")]
        int? release = null)
    {
        if (release is { } number)
        {
            var releaseDto = await GetReleaseAsync(number);
            var content = await ThemeAppService.GetReleaseFileAsync(releaseDto.Id, path);

            return new SiteThemeFileContent { Path = content.Path, IsAsset = content.IsAsset, Body = content.Body, Release = number };
        }

        var file = await ThemeAppService.GetFileAsync(path);
        return new SiteThemeFileContent
        {
            Path = file.Path,
            IsAsset = file.IsAsset,
            Body = file.Body,
            SyntaxErrors = file.SyntaxErrors.Select(Format).ToArray()
        };
    }

    [McpServerTool(Name = "site_save_theme_file", Title = "Save a theme file", Idempotent = true)]
    [Description(
        "Creates or overwrites one file of the theme's working copy. The site does not change: publish with " +
        "site_publish_theme. A path without an extension is a Scriban template (https://github.com/scriban/scriban): " +
        "'{{ }}' for output, '{{ if }}', '{{ for }}' for logic. A page uses a template when the page's 'template' value " +
        "equals the template's path. A layout places the page's output with '{{ body }}' and must call " +
        "'{{ site_head }}' inside <head>. A page template chooses its layout in front matter: the first line '+++', " +
        "then 'layout = \"layouts/landing\"' (or 'layout = \"none\"'), then '+++'; without one it uses the theme's " +
        "default layout. '{{ include \"blog/_sidebar\" }}' renders another template in place (a leading '_' is the " +
        "usual name for such a fragment, not a rule). A path with an extension (css, js, json, svg, txt, xml, ...) is a " +
        "static file, stored as it is and not parsed; a template links it with '{{ asset_url \"css/site.css\" }}'. " +
        "Every reference is a full path from the theme's root. A template that does not parse is saved anyway and its " +
        "errors are returned: fix them before publishing. Binary files (images, fonts) are not theme files: upload them " +
        "with site_upload_file.")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task<SiteThemeFileSaved> SaveThemeFileAsync(
        [Description("The file's path, e.g. 'Events', 'blog/post', '_card', 'layouts/landing' or 'css/site.css'.")]
        string path,
        [Description("The whole content of the file. Replaces the previous content entirely.")]
        string body)
    {
        var file = await ThemeAppService.SaveFileAsync(new SaveSiteThemeFileDto { Path = path, Body = body });

        return new SiteThemeFileSaved
        {
            Path = file.Path,
            IsAsset = file.IsAsset,
            SyntaxErrors = file.SyntaxErrors.Select(Format).ToArray()
        };
    }

    [McpServerTool(Name = "site_rename_theme_file", Title = "Rename a theme file", Idempotent = false)]
    [Description(
        "Moves a file of the theme's working copy to a new path. By default every literal include, asset_url and " +
        "front matter layout naming the old path is rewritten, and so is the default layout setting. Pages whose " +
        "'template' names the old path are not changed: they are listed, update them with site_update_page.")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task<SiteThemeRenameResultDto> RenameThemeFileAsync(
        [Description("The file's current path.")]
        string path,
        [Description("The new path. Keep the extension of a static file, and no extension for a template.")]
        string newPath,
        [Description("Whether to rewrite the references to the old path. Default true.")]
        bool updateReferences = true)
    {
        var result = await ThemeAppService.RenameFileAsync(new RenameSiteThemeFileDto
        {
            Path = path,
            NewPath = newPath,
            UpdateReferences = updateReferences
        });

        // The body is what was sent, or a rewritten copy of it: not worth the context it would take.
        result.File.Body = string.Empty;
        return result;
    }

    [McpServerTool(Name = "site_delete_theme_file", Title = "Delete a theme file", Destructive = true)]
    [Description(
        "Removes a file from the theme's working copy; the live site keeps it until the next publish. Returns what " +
        "still used it - pages, other files, the default layout setting - which the next check reports as errors or " +
        "Razor fallbacks. Confirm with the user first.")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task<SiteThemeFileUsagesDto> DeleteThemeFileAsync(
        [Description("The file's path, from site_list_theme_files.")]
        string path)
    {
        var usages = await ThemeAppService.GetFileUsagesAsync(path);
        await ThemeAppService.DeleteFileAsync(path);

        return usages;
    }

    [McpServerTool(Name = "site_update_theme", Title = "Update the theme's settings", Idempotent = true)]
    [Description(
        "Sets the theme's default layout: the layout of every page template whose front matter names none. A template " +
        "path such as 'layouts/default', or an empty string for no default layout. Like a file, the setting goes live " +
        "only with the next publish.")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task<SiteThemeDto> UpdateThemeAsync(
        [Description("The default layout's path, or an empty string for none.")]
        string defaultLayout)
    {
        return await ThemeAppService.UpdateAsync(new UpdateSiteThemeDto { DefaultLayout = defaultLayout });
    }

    [McpServerTool(Name = "site_check_theme", Title = "Check the theme", ReadOnly = true)]
    [Description(
        "Checks the whole working copy - syntax, front matter, references to missing files, layouts without site_head " +
        "or body, include cycles, includes that leave out a named argument the partial reads, pages whose template " +
        "is missing - and lists what publishing would change. Once those find no error, it also renders a sample of " +
        "the site's pages with the working copy (each page's own address, and one published content per content type " +
        "and language), and a template that fails to render - an unknown variable, say - is an error naming the " +
        "template, line, column and the address that failed; a rendered page that links to an #id nothing on it has " +
        "(a <use> of a symbol that is not there, an anchor to nowhere) is a warning. Errors block site_publish_theme; " +
        "warnings do not. " +
        "Returns the 'fingerprint' site_publish_theme needs.")]
    [Authorize(SiteAdminPermissions.Themes.Default)]
    public virtual async Task<SiteThemeCheckDto> CheckThemeAsync()
    {
        return await ThemeAppService.CheckAsync();
    }

    [McpServerTool(Name = "site_preview", Title = "Preview a page with the theme", ReadOnly = true)]
    [Description(
        "Renders a page of the site with the theme's working copy - or with a release - in place of the live release, " +
        "and returns the HTML; nothing is published and visitors see no change. Save first: it renders saved files " +
        "only. A template that fails at render time is returned as an error naming the template, line and column. " +
        "A page whose template is not in the theme is rendered by a Razor view and cannot be previewed here. HTML " +
        "longer than " + MaxPreviewHtmlLengthText + " characters is cut off: 'isTruncated' is then true and " +
        "'htmlLength' gives the full length. A page's HTML is a lot to read back: when you only need to know that " +
        "it renders, or to check its head, pass a small maxHtmlLength. It shows markup, not looks: to see how a page " +
        "really looks in a browser, use site_get_preview_link.")]
    [Authorize(SiteAdminPermissions.Themes.Default)]
    public virtual async Task<SiteThemePreview> PreviewAsync(
        [Description("A path of the site to render, e.g. '/about' or '/ja/blog/hello' - a language prefix included. " +
                     "Add '?page=2' for the second page of a paged list. Use site_resolve_path to check which page a " +
                     "path reaches.")]
        string path,
        [Description("A release number from site_list_releases. Omit for the working copy.")]
        int? release = null,
        [Description(
            "The most HTML characters to return, from the start of the page: 'htmlLength' still gives the whole " +
            "length. Omit for " + MaxPreviewHtmlLengthText + ", the largest. A page's head and the start of its body " +
            "are in the first 3000 or so.")]
        int? maxHtmlLength = null)
    {
        Guid? releaseId = release is { } number ? (await GetReleaseAsync(number)).Id : null;
        var limit = maxHtmlLength is { } requested ? Math.Clamp(requested, 1, MaxPreviewHtmlLength) : MaxPreviewHtmlLength;

        // A render failure is a business exception (Site:090008, carrying the template, line and column), which the
        // server's error envelope reports in full - nothing to catch here.
        var preview = await ThemeAppService.PreviewAsync(path, releaseId);
        var isTruncated = preview.Html.Length > limit;

        return new SiteThemePreview
        {
            Path = preview.Path,
            PageName = preview.PageName,
            CultureName = preview.CultureName,
            PageTemplate = preview.PageTemplate,
            Layout = preview.Layout,
            Release = release,
            Html = isTruncated ? preview.Html[..limit] : preview.Html,
            IsTruncated = isTruncated,
            HtmlLength = preview.Html.Length
        };
    }

    [McpServerTool(Name = "site_get_preview_link", Title = "Get a link to look at the site in a browser", ReadOnly = true)]
    [Description(
        "Returns a link that opens the site in a browser with the theme's working copy - or with a release - in place " +
        "of the live release, nothing published and visitors seeing no change. Use it to look at how a page really " +
        "looks (layout, colours, images, fonts, the way through the site), which the HTML of site_preview cannot " +
        "show; use site_preview to read a page's markup and head tags, which a screenshot does not show. Open the " +
        "url in a browser and take a screenshot. Opening it sets a cookie in that browser, and for the next couple of " +
        "hours every page and static file of the site opened there is rendered with that theme, so you can follow the " +
        "site's links; the working copy is read again on every page view, so after another site_save_theme_file only " +
        "reload. A page rendered by a template shows a banner at the top that says what is previewed. The link is good " +
        "for the minutes 'expiresAt' says and shows unpublished work to whoever holds it: give it to the user or " +
        "your own browser, nowhere else. A host that resolves the site from the request's address needs nothing " +
        "more; one that takes the tenant from a query or a header (a local development host) needs it added to the " +
        "address the same way as for any page of the site. Without a browser, give the link to the user.")]
    [Authorize(SiteAdminPermissions.Themes.Default)]
    public virtual async Task<SiteThemePreviewLink> GetPreviewLinkAsync(
        [Description("The path to open, e.g. '/about' or '/ja/blog/hello' - a language prefix included. Omit for the home page.")]
        string? path = null,
        [Description("A release number from site_list_releases. Omit for the working copy.")]
        int? release = null)
    {
        Guid? releaseId = release is { } number ? (await GetReleaseAsync(number)).Id : null;

        // Needs Dignite.Site.Public.Web in the same application: without it the application service says so
        // (Site:090007), which the server's error envelope reports in full.
        var link = await ThemeAppService.GetPreviewLinkAsync(path, releaseId);

        return new SiteThemePreviewLink
        {
            Url = link.Url,
            Path = link.Path,
            Release = release,
            ExpiresAt = link.ExpiresAt
        };
    }

    [McpServerTool(Name = "site_publish_theme", Title = "Publish the theme", Idempotent = false)]
    [Description(
        "Publishes the whole working copy as a new release and makes it live at once. Pass the 'fingerprint' of the " +
        "latest site_check_theme: the publish is refused if the working copy changed since, if the check finds an " +
        "error (rendering the sample pages again included), or if nothing changed since the live release. Confirm " +
        "with the user first - this changes the live site.")]
    [Authorize(SiteAdminPermissions.Themes.Publish)]
    public virtual async Task<SiteReleaseDto> PublishThemeAsync(
        [Description("The 'fingerprint' from site_check_theme.")]
        string fingerprint,
        [Description("A short note on what this release changes. Shown in the release list.")]
        string? comment = null)
    {
        return await ThemeAppService.PublishAsync(new PublishSiteThemeDto { Fingerprint = fingerprint, Comment = comment });
    }

    [McpServerTool(Name = "site_list_releases", Title = "List releases", ReadOnly = true)]
    [Description("Lists the theme's releases, newest first, with which one is live.")]
    [Authorize(SiteAdminPermissions.Themes.Default)]
    public virtual async Task<SiteReleaseDto[]> ListReleasesAsync()
    {
        return (await ThemeAppService.GetReleasesAsync()).Items.ToArray();
    }

    [McpServerTool(Name = "site_set_live_release", Title = "Make a release live", Idempotent = true)]
    [Description(
        "Makes an existing release the one the site renders, at once. Making an older release live is the rollback: do " +
        "that when a published change broke a page. The working copy is not changed. Confirm with the user first.")]
    [Authorize(SiteAdminPermissions.Themes.Publish)]
    public virtual async Task<SiteReleaseDto> SetLiveReleaseAsync(
        [Description("The release number, from site_list_releases.")]
        int release)
    {
        return await ThemeAppService.SetLiveReleaseAsync((await GetReleaseAsync(release)).Id);
    }

    [McpServerTool(Name = "site_restore_release", Title = "Restore a release into the working copy", Destructive = true)]
    [Description(
        "Replaces the theme's working copy with a release's files and default layout, deleting the files the release " +
        "does not have. Nothing goes live. Unpublished changes in the working copy are lost - confirm with the user first.")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task<string> RestoreReleaseAsync(
        [Description("The release number, from site_list_releases.")]
        int release)
    {
        await ThemeAppService.RestoreReleaseAsync((await GetReleaseAsync(release)).Id);

        return $"The working copy is now release #{release}. Nothing is published until site_publish_theme.";
    }

    protected virtual async Task<SiteReleaseDto> GetReleaseAsync(int number)
    {
        var releases = await ThemeAppService.GetReleasesAsync();

        return releases.Items.FirstOrDefault(release => release.Number == number)
               ?? throw new McpEntityNotFoundException(
                   "release",
                   number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                   hint: "Call site_list_releases for the releases this site has.");
    }

    protected static string Format(SiteThemeSyntaxErrorDto error)
    {
        return $"({error.Line},{error.Column}): {error.Message}";
    }
}

/// <summary>The theme's files and settings as a model reads them.</summary>
public class SiteThemeOverview
{
    public string? DefaultLayout { get; set; }

    public int? LiveReleaseNumber { get; set; }

    public SiteThemeFileEntry[] Files { get; set; } = Array.Empty<SiteThemeFileEntry>();
}

public class SiteThemeFileEntry
{
    public string Path { get; set; } = default!;

    public bool IsAsset { get; set; }

    /// <summary><c>Unchanged</c>, <c>Modified</c>, <c>Added</c> or <c>Deleted</c>, against the live release.</summary>
    public string Status { get; set; } = default!;

    public DateTime? LastModificationTime { get; set; }
}

public class SiteThemeFileContent
{
    public string Path { get; set; } = default!;

    public bool IsAsset { get; set; }

    public string Body { get; set; } = default!;

    /// <summary>The release it was read from, or null for the working copy.</summary>
    public int? Release { get; set; }

    /// <summary><c>(line,column): message</c> for each syntax error; empty when the template parses.</summary>
    public string[] SyntaxErrors { get; set; } = Array.Empty<string>();
}

public class SiteThemeFileSaved
{
    public string Path { get; set; } = default!;

    public bool IsAsset { get; set; }

    /// <summary><c>(line,column): message</c> for each syntax error; empty when the template parses.</summary>
    public string[] SyntaxErrors { get; set; } = Array.Empty<string>();
}

/// <summary>A link that opens the site with the theme in a browser - see <c>site_get_preview_link</c>.</summary>
public class SiteThemePreviewLink
{
    /// <summary>The address to open, absolute, with the token that starts the preview.</summary>
    public string Url { get; set; } = default!;

    /// <summary>The path it opens.</summary>
    public string Path { get; set; } = default!;

    /// <summary>The release it previews, or null for the working copy.</summary>
    public int? Release { get; set; }

    /// <summary>When the link stops starting a preview (UTC).</summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>A page rendered with the theme - see <c>site_preview</c>.</summary>
public class SiteThemePreview
{
    public string Path { get; set; } = default!;

    public string PageName { get; set; } = default!;

    public string CultureName { get; set; } = default!;

    /// <summary>The template the page rendered through.</summary>
    public string PageTemplate { get; set; } = default!;

    /// <summary>The layout it rendered in, or null for none.</summary>
    public string? Layout { get; set; }

    /// <summary>The release it rendered with, or null for the working copy.</summary>
    public int? Release { get; set; }

    /// <summary>The page's HTML, cut to <see cref="SiteThemeTools.MaxPreviewHtmlLength"/> characters.</summary>
    public string Html { get; set; } = default!;

    public bool IsTruncated { get; set; }

    public int HtmlLength { get; set; }
}
