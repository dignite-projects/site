using System.Threading.Tasks;
using Dignite.Site.Settings;
using Shouldly;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// How the enabled-languages setting is read, and where the default language comes from
/// (总体设计 §5.5). There is no separate default-language setting: the first entry is the default, so the
/// two answers cannot contradict each other.
/// </summary>
public class SiteLanguageProvider_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly SiteLanguageProvider _provider;
    private readonly TestSettingValueProvider _settings;

    public SiteLanguageProvider_Tests()
    {
        _provider = GetRequiredService<SiteLanguageProvider>();
        _settings = GetRequiredService<TestSettingValueProvider>();
    }

    [Fact]
    public async Task Should_Fall_Back_To_The_Platform_Default()
    {
        (await _provider.GetEnabledLanguagesAsync()).ShouldBe(new[] { "en" });
        (await _provider.GetDefaultLanguageAsync()).ShouldBe("en");
    }

    [Fact]
    public async Task Should_Read_The_Configured_List_In_Order()
    {
        _settings.Set(SiteSettings.EnabledLanguages, "fr, en , zh-Hans");

        (await _provider.GetEnabledLanguagesAsync()).ShouldBe(new[] { "fr", "en", "zh-Hans" });

        // First entry wins - a site that wants French unprefixed just lists it first.
        (await _provider.GetDefaultLanguageAsync()).ShouldBe("fr");
    }

    [Fact]
    public async Task Should_Normalize_And_Deduplicate()
    {
        _settings.Set(SiteSettings.EnabledLanguages, "EN,en,ZH-hans");

        (await _provider.GetEnabledLanguagesAsync()).ShouldBe(new[] { "en", "zh-Hans" });
    }

    /// <summary>
    /// The same reasoning as <c>CultureNameNormalizer</c>'s predefinedOnly: a typo must not become a
    /// language with its own URL space. It is skipped, and the rest of the list still works.
    /// </summary>
    [Fact]
    public async Task Should_Skip_Entries_That_Are_Not_Real_Cultures()
    {
        _settings.Set(SiteSettings.EnabledLanguages, "en,not-a-culture-at-all,fr");

        (await _provider.GetEnabledLanguagesAsync()).ShouldBe(new[] { "en", "fr" });
    }

    /// <summary>A site with no usable language would have no URLs at all, so there is always one.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",,,")]
    public async Task Should_Never_Return_An_Empty_List(string configured)
    {
        _settings.Set(SiteSettings.EnabledLanguages, configured);

        (await _provider.GetEnabledLanguagesAsync()).ShouldBe(new[] { SiteLanguageProvider.FallbackCultureName });
    }
}
