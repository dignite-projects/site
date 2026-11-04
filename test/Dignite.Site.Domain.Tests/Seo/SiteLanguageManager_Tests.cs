using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Settings;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// Setting the languages a site serves: the list as given, in order and in canonical form, refused when it holds
/// nothing or something that is not a language - not repaired the way reading the setting repairs it.
/// </summary>
public class SiteLanguageManager_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly SiteLanguageManager _manager;
    private readonly SiteLanguageProvider _provider;
    private readonly FakeSiteSettingWriter _settingWriter;

    public SiteLanguageManager_Tests()
    {
        _manager = GetRequiredService<SiteLanguageManager>();
        _provider = GetRequiredService<SiteLanguageProvider>();
        _settingWriter = GetRequiredService<FakeSiteSettingWriter>();
    }

    [Fact]
    public async Task Should_Store_The_Languages_In_Order_And_Make_The_First_The_Default()
    {
        var stored = await _manager.SetEnabledLanguagesAsync(new[] { "zh-Hans", "en" });

        stored.ShouldBe(new[] { "zh-Hans", "en" });
        (await _provider.GetEnabledLanguagesAsync()).ShouldBe(new[] { "zh-Hans", "en" });
        (await _provider.GetDefaultLanguageAsync()).ShouldBe("zh-Hans");
    }

    [Fact]
    public async Task Should_Store_Canonical_Tags_Without_Duplicates()
    {
        var stored = await _manager.SetEnabledLanguagesAsync(new[] { " ZH-hans ", "EN", "en", "", "zh-Hans" });

        stored.ShouldBe(new[] { "zh-Hans", "en" });
        _settingWriter.Writes.ShouldHaveSingleItem().Value.ShouldBe("zh-Hans,en");
    }

    /// <summary>Where the value lands - the tenant's own, or the host's default - is the writer's; the manager names the setting.</summary>
    [Fact]
    public async Task Should_Store_The_List_As_The_Enabled_Languages_Setting()
    {
        await _manager.SetEnabledLanguagesAsync(new[] { "en", "ja" });

        var write = _settingWriter.Writes.ShouldHaveSingleItem();
        write.Name.ShouldBe(SiteSettings.EnabledLanguages);
        write.Value.ShouldBe("en,ja");
    }

    [Fact]
    public async Task Should_Refuse_A_List_With_No_Language()
    {
        foreach (var cultureNames in new[] { Array.Empty<string>(), new[] { "", "  " } })
        {
            var exception = await Should.ThrowAsync<BusinessException>(() => _manager.SetEnabledLanguagesAsync(cultureNames));

            exception.Code.ShouldBe(SiteErrorCodes.SiteLanguagesRequired);
        }

        _settingWriter.Writes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Refuse_A_Tag_That_Is_Not_A_Language_And_Store_Nothing()
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => _manager.SetEnabledLanguagesAsync(new[] { "en", "not-a-language" }));

        exception.Code.ShouldBe(SiteErrorCodes.SiteLanguageNotRecognized);
        exception.Data["CultureName"].ShouldBe("not-a-language");
        _settingWriter.Writes.ShouldBeEmpty();
    }
}
