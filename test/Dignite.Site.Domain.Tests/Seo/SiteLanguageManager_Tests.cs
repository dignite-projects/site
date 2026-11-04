using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
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
    private readonly InMemorySettingManagementStore _settingStore;

    public SiteLanguageManager_Tests()
    {
        _manager = GetRequiredService<SiteLanguageManager>();
        _provider = GetRequiredService<SiteLanguageProvider>();
        _settingStore = GetRequiredService<InMemorySettingManagementStore>();
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
        _settingStore.Stored.ShouldHaveSingleItem().Value.ShouldBe("zh-Hans,en");
    }

    [Fact]
    public async Task Should_Store_The_Host_Sites_List_As_The_Global_Value()
    {
        await _manager.SetEnabledLanguagesAsync(new[] { "en", "ja" });

        var stored = _settingStore.Stored.ShouldHaveSingleItem();
        stored.Name.ShouldBe(SiteSettings.EnabledLanguages);
        stored.Value.ShouldBe("en,ja");
        stored.ProviderName.ShouldBe("G");
        stored.ProviderKey.ShouldBeNull();
    }

    /// <summary>Forced: a tenant that sets the very value the host has keeps it when the host changes its own.</summary>
    [Fact]
    public async Task Should_Store_A_Tenants_List_For_The_Tenant_And_Keep_It_Its_Own()
    {
        var tenantId = Guid.NewGuid();

        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            // "en" is what a site serves when nothing is set: the value the tenant would otherwise inherit.
            await _manager.SetEnabledLanguagesAsync(new[] { "en" });
            (await _provider.GetEnabledLanguagesAsync()).ShouldBe(new[] { "en" });
        }

        var stored = _settingStore.Stored.ShouldHaveSingleItem();
        stored.Value.ShouldBe("en");
        stored.ProviderName.ShouldBe("T");
        stored.ProviderKey.ShouldBe(tenantId.ToString());
    }

    [Fact]
    public async Task Should_Refuse_A_List_With_No_Language()
    {
        foreach (var cultureNames in new[] { Array.Empty<string>(), new[] { "", "  " } })
        {
            var exception = await Should.ThrowAsync<BusinessException>(() => _manager.SetEnabledLanguagesAsync(cultureNames));

            exception.Code.ShouldBe(SiteErrorCodes.SiteLanguagesRequired);
        }

        _settingStore.Stored.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Refuse_A_Tag_That_Is_Not_A_Language_And_Store_Nothing()
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => _manager.SetEnabledLanguagesAsync(new[] { "en", "not-a-language" }));

        exception.Code.ShouldBe(SiteErrorCodes.SiteLanguageNotRecognized);
        exception.Data["CultureName"].ShouldBe("not-a-language");
        _settingStore.Stored.ShouldBeEmpty();
    }
}
