using System;
using System.Threading.Tasks;
using Dignite.Site.Templates;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Templates;

/// <summary>
/// Tenant-authored Scriban templates: a header per name plus immutable numbered versions and a pointer to
/// the published one. Saving rejects a body with a syntax error (<c>SiteTemplateSyntaxException</c>, with
/// line and column), and the template named <c>layout</c> must call <c>site_head</c>.
/// </summary>
public interface ISiteTemplateAdminAppService : IApplicationService
{
    Task<SiteTemplateDto> GetAsync(Guid id);

    /// <summary>
    /// Finds a template by its tenant-unique name, or null. The lookup the MCP tool surface addresses
    /// templates by. The name is normalized the way it is stored (<c>SiteTemplate.NormalizeName</c>), so a
    /// leading slash or different casing finds the same template.
    /// </summary>
    Task<SiteTemplateDto?> FindByNameAsync(string name);

    Task<ListResultDto<SiteTemplateDto>> GetListAsync(GetSiteTemplateListInput input);

    /// <summary>Creates the template with its first version, published unless <c>Publish</c> is false.</summary>
    Task<SiteTemplateDto> CreateAsync(CreateSiteTemplateDto input);

    /// <summary>Appends a version, and publishes it when <c>Publish</c> is true.</summary>
    Task<SiteTemplateDto> UpdateAsync(Guid id, UpdateSiteTemplateDto input);

    /// <summary>
    /// Makes an existing version the one pages render. Publishing an older version is the rollback.
    /// Covered by the Update permission.
    /// </summary>
    Task<SiteTemplateDto> PublishAsync(Guid id, int versionNumber);

    /// <summary>
    /// The versions, newest first. <see cref="SiteTemplateVersionDto.Body"/> is empty in this list, to
    /// keep it light - read one version with <see cref="GetVersionAsync"/> for its body.
    /// </summary>
    Task<ListResultDto<SiteTemplateVersionDto>> GetVersionsAsync(Guid id);

    /// <summary>One version with its body. <c>EntityNotFoundException</c> when there is no such number.</summary>
    Task<SiteTemplateVersionDto> GetVersionAsync(Guid id, int versionNumber);

    /// <summary>
    /// Renders the site page at <paramref name="path"/> (<c>/about</c>, <c>/ja/blog/hello</c>) with version
    /// <paramref name="versionNumber"/> in place of the template's published version, and returns its HTML.
    /// The published <c>layout</c> still wraps a previewed page template; previewing <c>layout</c> itself wraps
    /// the page's published template (or an empty body, when the page is still rendered by Razor). Nothing is
    /// published or cached for visitors, so a draft can be checked before it goes live.
    /// <para>
    /// Needs <c>Dignite.Site.Public.Web</c> in the same application - it is what renders pages - and fails
    /// with <c>Site:090007</c> without it; a path no page answers fails with <c>Site:090006</c>, a missing
    /// version with <c>EntityNotFoundException</c>. A template that fails to render fails the call with the
    /// renderer's error. Covered by the Default permission, like reading a version.
    /// </para>
    /// </summary>
    Task<SiteTemplatePreviewDto> PreviewAsync(Guid id, int versionNumber, string path);

    /// <summary>
    /// Who uses the template: the routable pages it takes part in rendering, each with its own path and - for
    /// a page with a slug - a sample content's path to preview the detail view with, and the templates that
    /// refer to it. A page template is used by the pages whose <c>Template</c> is its name, the <c>layout</c>
    /// by every page, a partial or an asset by the templates whose current version (the published one, else
    /// the newest) names it in an <c>include "..."</c> or <c>asset_url "..."</c> with a literal name - and so
    /// by those templates' pages, followed through partials that include partials. A name computed at run
    /// time cannot be known and is not counted. Covered by the Default permission.
    /// </summary>
    Task<SiteTemplateUsageDto> GetUsageAsync(Guid id);

    /// <summary>
    /// The routable pages whose <c>Template</c> names no published template - the site renders them with the
    /// Razor view of that name - with the template of that name when one exists but was never published.
    /// Covered by the Default permission.
    /// </summary>
    Task<ListResultDto<SiteTemplateUnmatchedPageDto>> GetUnmatchedPagesAsync();

    /// <summary>
    /// A signed link that opens the site at <paramref name="path"/> (<c>/about</c>, <c>/ja/blog/hello</c>; empty
    /// for the home page) in a browser with version <paramref name="versionNumber"/> in place of the template's
    /// published one - on that page and every page the visitor opens after it, whatever the template's kind:
    /// a page template, the <c>layout</c>, a partial or an asset. The link's token is valid for a few minutes
    /// and is exchanged for a preview cookie on first use; nothing is published or cached for visitors.
    /// <para>
    /// The link's origin is the tenant's primary domain when one is configured, else the request this is
    /// called from. Needs <c>Dignite.Site.Public.Web</c> in the same application - it signs the token with
    /// the keys it then verifies it with - and fails with <c>Site:090009</c> without it; a missing version
    /// fails with <c>EntityNotFoundException</c>. Covered by the Default permission, like reading a version.
    /// </para>
    /// </summary>
    Task<SiteTemplatePreviewLinkDto> GetPreviewLinkAsync(Guid id, int versionNumber, string path);

    /// <summary>
    /// Deletes the template with all its versions; a <c>Page.Template</c> of that name falls back to the
    /// Razor view of the same name.
    /// </summary>
    Task DeleteAsync(Guid id);
}
