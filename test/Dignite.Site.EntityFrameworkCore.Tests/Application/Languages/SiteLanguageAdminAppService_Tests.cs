using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Settings;
using Shouldly;
using Xunit;

namespace Dignite.Site.Admin.Languages;

public class SiteLanguageAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ISiteLanguageAdminAppService _languageAppService;
    private readonly InMemorySettingManagementStore _settingStore;

    public SiteLanguageAdminAppService_Tests()
    {
        _languageAppService = GetRequiredService<ISiteLanguageAdminAppService>();
        _settingStore = GetRequiredService<InMemorySettingManagementStore>();
    }

    [Fact]
    public async Task Should_Serve_Only_English_Until_Languages_Are_Set()
    {
        var languages = await _languageAppService.GetAsync();

        languages.EnabledLanguages.ShouldBe(new[] { "en" });
        languages.DefaultLanguage.ShouldBe("en");
    }

    [Fact]
    public async Task Should_Read_Back_The_Languages_It_Set()
    {
        var updated = await _languageAppService.UpdateAsync(new UpdateSiteLanguagesDto
        {
            EnabledLanguages = new() { "ja", "en", "zh-Hans" }
        });

        updated.EnabledLanguages.ShouldBe(new[] { "ja", "en", "zh-Hans" });
        updated.DefaultLanguage.ShouldBe("ja");

        var read = await _languageAppService.GetAsync();
        read.EnabledLanguages.ShouldBe(updated.EnabledLanguages);
        read.DefaultLanguage.ShouldBe("ja");
        _settingStore.Stored.ShouldHaveSingleItem().Value.ShouldBe("ja,en,zh-Hans");
    }
}
