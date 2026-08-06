using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Dignite.Site.Admin.ContentTypes;
using Dignite.Site.Admin.Contents;
using Dignite.Site.Admin.Fields;
using Dignite.Site.Admin.Pages;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Mcp.Errors;
using Dignite.Site.Pages;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Validation;

namespace Dignite.Site.Mcp.Naming;

/// <summary>
/// Turns the names an MCP tool is addressed by into the ids the application services take
/// (总体设计 §6.2.4).
/// <para>
/// <b>Names, never Guids.</b> <c>Page.Name</c> is unique per tenant, <c>ContentType.Name</c> per page and
/// <c>Field.Name</c> per tenant, so every level already has a natural string key. Making a language model
/// carry Guids costs tokens and invites hallucination, and buys nothing.
/// </para>
/// <para>
/// <b>Resolution goes through the application services, not the repositories.</b> That is what keeps the
/// <c>[Authorize]</c> gate in the path: a caller who may not read pages must not be able to learn whether
/// a page exists by watching which error a tool returns.
/// </para>
/// </summary>
public class SiteMcpNameResolver : ITransientDependency
{
    protected IPageAdminAppService PageAppService { get; }

    protected IContentTypeAdminAppService ContentTypeAppService { get; }

    protected IFieldAdminAppService FieldAppService { get; }

    protected IContentAdminAppService ContentAppService { get; }

    public SiteMcpNameResolver(
        IPageAdminAppService pageAppService,
        IContentTypeAdminAppService contentTypeAppService,
        IFieldAdminAppService fieldAppService,
        IContentAdminAppService contentAppService)
    {
        PageAppService = pageAppService;
        ContentTypeAppService = contentTypeAppService;
        FieldAppService = fieldAppService;
        ContentAppService = contentAppService;
    }

    public virtual async Task<PageDto> GetPageAsync(string page)
    {
        return await PageAppService.FindByNameAsync(page)
               ?? throw new McpEntityNotFoundException("page", page);
    }

    public virtual async Task<ContentTypeDto> GetContentTypeAsync(string page, string contentType)
    {
        var pageDto = await GetPageAsync(page);
        return await GetContentTypeAsync(pageDto, contentType);
    }

    public virtual async Task<ContentTypeDto> GetContentTypeAsync(PageDto page, string contentType)
    {
        return await ContentTypeAppService.FindByNameAsync(page.Id, contentType)
               ?? throw new McpEntityNotFoundException("content type", contentType, $"under page '{page.Name}'");
    }

    public virtual async Task<FieldDto> GetFieldAsync(string field)
    {
        return await FieldAppService.FindByNameAsync(field)
               ?? throw new McpEntityNotFoundException("field", field);
    }

    /// <summary>
    /// Finds the content addressed by page, language and slug.
    /// <para>
    /// An empty <paramref name="slug"/> is a legitimate address, not a missing argument: it is how the
    /// single content of a home or "about" page is named (总体设计 §2.4). So the not-found message spells
    /// the slug out rather than interpolating it invisibly.
    /// </para>
    /// <para>
    /// Tries the slug as given, then its normalized form. The second attempt is what makes the write and
    /// read paths agree: <c>create_content</c> stores <c>McpSlug.Normalize(slug)</c> and its own
    /// description invites a title ("A title is acceptable here and will be turned into a slug"), so a
    /// client that echoes back what it passed to create - the obvious thing to do - is addressing with a
    /// string that was never stored. Trying the raw value first keeps contents written through the admin
    /// API, whose slugs are stored verbatim and need not be normalized at all, reachable.
    /// </para>
    /// </summary>
    public virtual async Task<ContentDto> GetContentAsync(PageDto page, string cultureName, string slug)
    {
        // Checked before the lookup, because afterwards it is indistinguishable from a genuine miss: the
        // read path returns null for a culture no CultureInfo recognizes, and the not-found message that
        // follows puts the SLUG in every machine-readable field. A model told "there is no content
        // 'my-trip'" re-reads the schema, finds my-trip listed, and retries the identical call - while the
        // one wrong argument was the language all along.
        if (!CultureNameNormalizer.TryNormalize(cultureName, out _))
        {
            throw new AbpValidationException(
                $"'{cultureName}' is not a language tag.",
                new List<ValidationResult>
                {
                    new(
                        "Use one of the site's enabled languages exactly as get_site_schema reports them "
                        + "in 'enabledLanguages' - they are IETF tags such as 'en' or 'zh-Hans', not "
                        + "language names.",
                        new[] { "cultureName" })
                });
        }

        // Deliberately not coerced when null - the app service rejects that with a validation error
        // naming the field, which is what a client can act on. Reading "omitted" as "empty" here would
        // quietly answer with the page's own content instead.
        var content = await ContentAppService.FindBySlugAsync(page.Id, cultureName, slug);

        if (content == null)
        {
            var normalized = McpSlug.NormalizeAddressOrNull(slug);

            if (normalized != null)
            {
                content = await ContentAppService.FindBySlugAsync(page.Id, cultureName, normalized);
            }
        }

        return content
               ?? throw new McpEntityNotFoundException(
                   "content",
                   slug.Length == 0 ? "(empty slug - the page's own content)" : slug,
                   $"under page '{page.Name}' in '{cultureName}'");
    }

    public virtual async Task<(PageDto Page, ContentDto Content)> GetContentAsync(
        string page,
        string cultureName,
        string slug)
    {
        var pageDto = await GetPageAsync(page);
        return (pageDto, await GetContentAsync(pageDto, cultureName, slug));
    }
}
