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
    /// Deletes the template with all its versions; a <c>Page.Template</c> of that name falls back to the
    /// Razor view of the same name.
    /// </summary>
    Task DeleteAsync(Guid id);
}
