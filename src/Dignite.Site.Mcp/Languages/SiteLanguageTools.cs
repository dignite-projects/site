using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Site.Admin.Languages;
using Dignite.Site.Admin.Permissions;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Languages;

/// <summary>
/// The languages the site serves. Read from <c>site_get_schema</c>, which lists them, so only the write is a tool: another
/// read would be a second place to learn the same list.
/// </summary>
[McpServerToolType]
public class SiteLanguageTools : ITransientDependency
{
    protected ISiteLanguageAdminAppService LanguageAppService { get; }

    public SiteLanguageTools(ISiteLanguageAdminAppService languageAppService)
    {
        LanguageAppService = languageAppService;
    }

    [McpServerTool(Name = "site_set_languages", Title = "Set the site's languages", Idempotent = true)]
    [Description(
        "Sets the languages the site serves, as the whole list in order: the first is the default language, whose pages " +
        "have no language prefix in their address, and each other one is served under /<language>/. It replaces the " +
        "list, so read the current one from site_get_schema (enabledLanguages) and send it back with your changes. " +
        "A site that has not been set up serves just 'en'; set it before writing content in another language, and put " +
        "the language of most of the content first. Changing the default language moves the address of every page - " +
        "the old default's pages gain their prefix, the new one's lose it - and a language left out stops being served: " +
        "its contents are kept, but no visitor can reach them until it is added back. Confirm with the user first. " +
        "Returns the languages as stored, tags in their canonical form such as 'zh-Hans'.")]
    [Authorize(SiteAdminPermissions.Languages.Update)]
    public virtual Task<SiteLanguagesDto> SetLanguagesAsync(
        [Description("BCP 47 language tags in order, e.g. [\"zh-Hans\", \"en\"]. At least one; the first is the default.")]
        List<string> enabledLanguages)
    {
        return LanguageAppService.UpdateAsync(new UpdateSiteLanguagesDto { EnabledLanguages = enabledLanguages });
    }
}
