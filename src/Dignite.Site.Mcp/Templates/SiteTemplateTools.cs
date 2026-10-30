using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Admin.Templates;
using Dignite.Site.Mcp.Naming;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Templates;

/// <summary>
/// Templates a tenant (or an AI) writes in Scriban, stored in the database as numbered immutable versions
/// with a published pointer. A template is addressed by its name - the same string a page's
/// <c>Page.Template</c> holds - so no tool takes an id.
/// <para>
/// Templates are not part of <c>site_get_schema</c> / <c>site://schema</c>: they are rendering, not site
/// structure, and a model that is not touching rendering should not pay for them in every schema read.
/// <c>site_list_templates</c> is how their names are found.
/// </para>
/// </summary>
[McpServerToolType]
public class SiteTemplateTools : ITransientDependency
{
    /// <summary>
    /// The most HTML <c>site_preview_template</c> returns, in characters: enough to check a page - its head,
    /// its markup, the content it shows - and small enough to sit in a model's context next to everything
    /// else it is working on. A longer page is cut off and says so.
    /// </summary>
    public const int MaxPreviewHtmlLength = 32 * 1024;

    /// <summary>
    /// <see cref="MaxPreviewHtmlLength"/> as text, for the tool description - an attribute argument must be a
    /// constant, and a number cannot be turned into a constant string. Keep the two equal.
    /// </summary>
    private const string MaxPreviewHtmlLengthText = "32768";

    protected ISiteTemplateAdminAppService TemplateAppService { get; }

    protected SiteMcpNameResolver NameResolver { get; }

    public SiteTemplateTools(ISiteTemplateAdminAppService templateAppService, SiteMcpNameResolver nameResolver)
    {
        TemplateAppService = templateAppService;
        NameResolver = nameResolver;
    }

    [McpServerTool(Name = "site_list_templates", Title = "List templates", ReadOnly = true)]
    [Description(
        "Lists the templates written for this site (Scriban syntax), with which version of each is " +
        "published. A page renders through the published template whose name equals its 'template' value " +
        "(see site_create_page); a page whose template name has no published template here is rendered by " +
        "the Razor view of that name instead. The template named 'layout' is special: it wraps every page. " +
        "A null publishedVersionNumber means the template is a draft that nothing renders.")]
    [Authorize(SiteAdminPermissions.Templates.Default)]
    public virtual async Task<SiteTemplateSummary[]> ListTemplatesAsync(
        [Description("Free-text filter over the template name. Omit for all.")]
        string? filter = null)
    {
        var templates = await TemplateAppService.GetListAsync(new GetSiteTemplateListInput { Filter = filter });

        return templates.Items.Select(SiteTemplateSummary.From).ToArray();
    }

    [McpServerTool(Name = "site_get_template", Title = "Get a template", ReadOnly = true)]
    [Description(
        "Reads one template's source (Scriban syntax). Returns the published version's body by default; " +
        "pass 'version' to read another one, for instance the one you staged with publish=false or an " +
        "older one before rolling back. A template that has never been published returns its latest " +
        "version instead, with isPublished false. 'versions' lists every saved version (without bodies), " +
        "newest first. Read the current body before changing a template: site_save_template replaces it " +
        "with exactly what you send.")]
    [Authorize(SiteAdminPermissions.Templates.Default)]
    public virtual async Task<SiteTemplateDetail> GetTemplateAsync(
        [Description("The template's name, e.g. 'layout' or 'Blog/Article', from site_list_templates.")]
        string template,
        [Description("The version number to read. Omit for the published version (or the latest, if none is published).")]
        int? version = null)
    {
        var current = await NameResolver.GetTemplateAsync(template);

        var versionNumber = version ?? current.PublishedVersionNumber ?? current.LatestVersionNumber;

        var body = await TemplateAppService.GetVersionAsync(current.Id, versionNumber);
        var versions = await TemplateAppService.GetVersionsAsync(current.Id);

        return new SiteTemplateDetail
        {
            Name = current.Name,
            PublishedVersionNumber = current.PublishedVersionNumber,
            LatestVersionNumber = current.LatestVersionNumber,
            LastModificationTime = current.LastModificationTime,
            Version = body.Number,
            IsPublished = current.PublishedVersionNumber == body.Number,
            Comment = body.Comment,
            Body = body.Body,
            Versions = versions.Items
                .Select(item => new SiteTemplateVersionSummary
                {
                    Number = item.Number,
                    Comment = item.Comment,
                    CreationTime = item.CreationTime,
                    IsPublished = current.PublishedVersionNumber == item.Number
                })
                .ToArray()
        };
    }

    [McpServerTool(Name = "site_save_template", Title = "Save a template", Idempotent = false)]
    [Description(
        "Creates the template, or - when one of that name exists - saves 'body' as its next version. " +
        "Versions are never edited, so every save is kept and can be rolled back with site_publish_template. " +
        "The body is Scriban syntax (https://github.com/scriban/scriban): '{{ }}' for output, '{{ if }}', " +
        "'{{ for }}' for logic. A page uses a template when the page's 'template' value equals the " +
        "template's name; the template named 'layout' wraps every page, places the page's rendered " +
        "content with '{{ body }}', and MUST call '{{ site_head }}' inside <head> or it is rejected. " +
        "The body is parsed before it is stored: a syntax error is returned with its line and column, " +
        "and nothing is saved - fix the reported lines and send the whole body again. By default the new " +
        "version goes live at once, so the site changes as soon as this returns; pass publish=false to " +
        "stage it and publish it later with site_publish_template. Needs both the Create and the Update " +
        "template permission, since the same call can do either.")]
    [Authorize(SiteAdminPermissions.Templates.Create)]
    [Authorize(SiteAdminPermissions.Templates.Update)]
    public virtual async Task<SiteTemplateSummary> SaveTemplateAsync(
        [Description("The template's name, e.g. 'layout', 'Default' or 'Blog/Article'. Unique across the site.")]
        string template,
        [Description("The full Scriban source of the template. Replaces the previous body entirely.")]
        string body,
        [Description("A short note on what this version changes. Shown in the version list.")]
        string? comment = null,
        [Description("Whether this version goes live now. False saves it without changing what the site renders.")]
        bool publish = true)
    {
        var existing = await TemplateAppService.FindByNameAsync(template);

        var saved = existing == null
            ? await TemplateAppService.CreateAsync(new CreateSiteTemplateDto
            {
                Name = template,
                Body = body,
                Comment = comment,
                Publish = publish
            })
            : await TemplateAppService.UpdateAsync(existing.Id, new UpdateSiteTemplateDto
            {
                Body = body,
                Comment = comment,
                Publish = publish
            });

        return SiteTemplateSummary.From(saved);
    }

    [McpServerTool(Name = "site_publish_template", Title = "Publish a template version", Idempotent = true)]
    [Description(
        "Makes an existing version of a template the one the site renders. Publishing a version older than " +
        "the current one is the rollback: do that when a published change broke a page. Takes effect at once.")]
    [Authorize(SiteAdminPermissions.Templates.Update)]
    public virtual async Task<SiteTemplateSummary> PublishTemplateAsync(
        [Description("The template's name, from site_list_templates.")]
        string template,
        [Description("The version number to publish, from site_get_template's 'versions'.")]
        int version)
    {
        var current = await NameResolver.GetTemplateAsync(template);

        return SiteTemplateSummary.From(await TemplateAppService.PublishAsync(current.Id, version));
    }

    [McpServerTool(Name = "site_preview_template", Title = "Preview a template version", ReadOnly = true)]
    [Description(
        "Renders a page of the site with one saved version of a template in place of the published one, and " +
        "returns the HTML - nothing is published and visitors see no change. Previewing a page template keeps " +
        "the published 'layout' around it; previewing 'layout' wraps the page's published template. It renders " +
        "a SAVED version only: to check a change, save it first with site_save_template and publish=false, " +
        "preview that version, then publish it with site_publish_template. A template that fails at render " +
        "time is returned as an error naming the template, line and column - fix the body, save again, preview " +
        "again. 'pageTemplate' is the template the path's page normally renders through: when it is not the " +
        "previewed template (and that is not 'layout'), the live page would not use what you are previewing. " +
        "HTML longer than " + MaxPreviewHtmlLengthText + " characters is cut off: 'isTruncated' is then true " +
        "and 'htmlLength' gives the full length.")]
    [Authorize(SiteAdminPermissions.Templates.Default)]
    public virtual async Task<SiteTemplatePreview> PreviewTemplateAsync(
        [Description("The template's name, e.g. 'layout' or 'Blog/Article', from site_list_templates.")]
        string template,
        [Description("A path of the site to render, e.g. '/about' or '/ja/blog/hello' - a language prefix " +
                     "included. Use site_resolve_path to check which page a path reaches.")]
        string path,
        [Description("The version number to render. Omit for the published version (or the latest, if none is published).")]
        int? version = null)
    {
        var current = await NameResolver.GetTemplateAsync(template);

        var versionNumber = version ?? current.PublishedVersionNumber ?? current.LatestVersionNumber;

        // A render failure is a business exception (Site:090008, carrying the template, line and column), which
        // the server's error envelope reports in full - nothing to catch here.
        var preview = await TemplateAppService.PreviewAsync(current.Id, versionNumber, path);
        var isTruncated = preview.Html.Length > MaxPreviewHtmlLength;

        return new SiteTemplatePreview
        {
            Name = current.Name,
            Version = versionNumber,
            IsPublished = current.PublishedVersionNumber == versionNumber,
            Path = preview.Path,
            PageName = preview.PageName,
            CultureName = preview.CultureName,
            PageTemplate = preview.PageTemplate,
            Html = isTruncated ? preview.Html[..MaxPreviewHtmlLength] : preview.Html,
            IsTruncated = isTruncated,
            HtmlLength = preview.Html.Length
        };
    }

    [McpServerTool(Name = "site_delete_template", Title = "Delete a template", Destructive = true)]
    [Description(
        "Deletes a template with all of its versions. Pages that used it are not deleted: a page whose " +
        "'template' value names it falls back to the Razor view of that name (or fails to render if the " +
        "site has none). Deleting 'layout' removes the layout that wrapped every page. If you only want to " +
        "undo a change, publish an older version instead (site_publish_template). Confirm with the user first.")]
    [Authorize(SiteAdminPermissions.Templates.Delete)]
    public virtual async Task<string> DeleteTemplateAsync(
        [Description("The template's name, from site_list_templates.")]
        string template)
    {
        var current = await NameResolver.GetTemplateAsync(template);

        await TemplateAppService.DeleteAsync(current.Id);

        return $"Deleted template '{current.Name}' with all its versions. Pages using that name now render " +
               "through the Razor view of the same name.";
    }
}

/// <summary>A template's header as a model reads it - the audit columns of <see cref="SiteTemplateDto"/> are noise there.</summary>
public class SiteTemplateSummary
{
    public string Name { get; set; } = default!;

    /// <summary>The version the site renders, or null when the template is a draft.</summary>
    public int? PublishedVersionNumber { get; set; }

    public int LatestVersionNumber { get; set; }

    public DateTime? LastModificationTime { get; set; }

    public static SiteTemplateSummary From(SiteTemplateDto dto)
    {
        return new SiteTemplateSummary
        {
            Name = dto.Name,
            PublishedVersionNumber = dto.PublishedVersionNumber,
            LatestVersionNumber = dto.LatestVersionNumber,
            LastModificationTime = dto.LastModificationTime
        };
    }
}

/// <summary>One template with the body of one of its versions - see <c>site_get_template</c>.</summary>
public class SiteTemplateDetail
{
    public string Name { get; set; } = default!;

    public int? PublishedVersionNumber { get; set; }

    public int LatestVersionNumber { get; set; }

    public DateTime? LastModificationTime { get; set; }

    /// <summary>The number of the version whose <see cref="Body"/> this is.</summary>
    public int Version { get; set; }

    /// <summary>
    /// Whether <see cref="Version"/> is the one the site renders. False for a staged or an older version,
    /// and for a template that was never published (then the latest version is returned).
    /// </summary>
    public bool IsPublished { get; set; }

    public string? Comment { get; set; }

    public string Body { get; set; } = default!;

    /// <summary>Every saved version, newest first, without bodies.</summary>
    public SiteTemplateVersionSummary[] Versions { get; set; } = Array.Empty<SiteTemplateVersionSummary>();
}

/// <summary>A page rendered with one version of a template - see <c>site_preview_template</c>.</summary>
public class SiteTemplatePreview
{
    /// <summary>The previewed template's name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>The number of the version that was rendered.</summary>
    public int Version { get; set; }

    /// <summary>Whether <see cref="Version"/> is the one the site renders now.</summary>
    public bool IsPublished { get; set; }

    /// <summary>The path that was rendered.</summary>
    public string Path { get; set; } = default!;

    /// <summary>The page the path resolved to.</summary>
    public string PageName { get; set; } = default!;

    /// <summary>The language the page was rendered in.</summary>
    public string CultureName { get; set; } = default!;

    /// <summary>
    /// The page's own template value. When it differs from <see cref="Name"/> (and <see cref="Name"/> is not
    /// <c>layout</c>), the live page does not render through what was previewed.
    /// </summary>
    public string PageTemplate { get; set; } = default!;

    /// <summary>The page's HTML, cut to <see cref="SiteTemplateTools.MaxPreviewHtmlLength"/> characters.</summary>
    public string Html { get; set; } = default!;

    /// <summary>Whether <see cref="Html"/> was cut off.</summary>
    public bool IsTruncated { get; set; }

    /// <summary>The length of the whole HTML, in characters, before any cut.</summary>
    public int HtmlLength { get; set; }
}

public class SiteTemplateVersionSummary
{
    public int Number { get; set; }

    public string? Comment { get; set; }

    public DateTime CreationTime { get; set; }

    public bool IsPublished { get; set; }
}
