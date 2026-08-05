using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Settings;
using Xunit;

namespace Dignite.Site.Settings;

public class SiteSettingDefinitionProvider_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly ISettingDefinitionManager _settingDefinitionManager;
    private readonly ISettingProvider _settingProvider;

    public SiteSettingDefinitionProvider_Tests()
    {
        _settingDefinitionManager = GetRequiredService<ISettingDefinitionManager>();
        _settingProvider = GetRequiredService<ISettingProvider>();
    }

    [Theory]
    [InlineData(SiteSettings.EnabledLanguages, "en")]
    [InlineData(SiteSettings.Robots.AllowIndexing, "true")]
    [InlineData(SiteSettings.Robots.AllowAiTraining, "false")]
    [InlineData(SiteSettings.Robots.AllowAiSearch, "true")]
    public async Task Should_Resolve_Platform_Default(string name, string expected)
    {
        (await _settingProvider.GetOrNullAsync(name)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(SiteSettings.EnabledLanguages)]
    [InlineData(SiteSettings.Robots.AllowIndexing)]
    [InlineData(SiteSettings.Robots.AllowAiTraining)]
    [InlineData(SiteSettings.Robots.AllowAiSearch)]
    public async Task Should_Not_Be_Locked_To_Tenant_Provider(string name)
    {
        // No .WithProviders("T"): an empty Providers list means every provider is allowed, which is what
        // lets a TenantId==null request (the host's own site) still resolve the Global value (总体设计 §4.3).
        var definition = await _settingDefinitionManager.GetOrNullAsync(name);

        definition.ShouldNotBeNull();
        definition!.Providers.ShouldBeEmpty();
    }
}
