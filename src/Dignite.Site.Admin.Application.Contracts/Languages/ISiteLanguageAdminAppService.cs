using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Languages;

/// <summary>
/// The languages the site serves: the <c>Site.EnabledLanguages</c> setting as a list, whose first language is the default.
/// <para>
/// A setting of the site already, set from ABP's Setting Management; this is the same value for a client that has no
/// setting UI to set it with - an MCP client building a site starts with a default language of <c>en</c> and no content in it.
/// </para>
/// </summary>
public interface ISiteLanguageAdminAppService : IApplicationService
{
    Task<SiteLanguagesDto> GetAsync();

    /// <summary>
    /// Sets the whole list. The first language is the default, whose pages carry no URL prefix, so changing it moves
    /// the address of every page; a language left out is no longer served, though its contents are kept.
    /// </summary>
    Task<SiteLanguagesDto> UpdateAsync(UpdateSiteLanguagesDto input);
}
