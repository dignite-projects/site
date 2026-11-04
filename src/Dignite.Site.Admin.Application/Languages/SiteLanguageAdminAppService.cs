using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Authorization;

namespace Dignite.Site.Admin.Languages;

[Authorize(SiteAdminPermissions.Languages.Default)]
public class SiteLanguageAdminAppService : SiteAdminAppService, ISiteLanguageAdminAppService
{
    protected SiteLanguageProvider LanguageProvider { get; }

    protected SiteLanguageManager LanguageManager { get; }

    public SiteLanguageAdminAppService(SiteLanguageProvider languageProvider, SiteLanguageManager languageManager)
    {
        LanguageProvider = languageProvider;
        LanguageManager = languageManager;
    }

    public virtual async Task<SiteLanguagesDto> GetAsync()
    {
        return Map(await LanguageProvider.GetEnabledLanguagesAsync());
    }

    [Authorize(SiteAdminPermissions.Languages.Update)]
    public virtual async Task<SiteLanguagesDto> UpdateAsync(UpdateSiteLanguagesDto input)
    {
        return Map(await LanguageManager.SetEnabledLanguagesAsync(input.EnabledLanguages));
    }

    protected virtual SiteLanguagesDto Map(System.Collections.Generic.IReadOnlyList<string> languages)
    {
        return new SiteLanguagesDto
        {
            EnabledLanguages = languages.ToList(),
            DefaultLanguage = languages[0]
        };
    }
}
