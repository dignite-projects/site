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
    /// Deletes the template with all its versions; a <c>Page.Template</c> of that name falls back to the
    /// Razor view of the same name.
    /// </summary>
    Task DeleteAsync(Guid id);
}
