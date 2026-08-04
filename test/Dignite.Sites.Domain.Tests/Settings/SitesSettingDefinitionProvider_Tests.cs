using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Settings;
using Xunit;

namespace Dignite.Sites.Settings;

public class SitesSettingDefinitionProvider_Tests : SitesDomainTestBase<SitesDomainTestModule>
{
    private readonly ISettingDefinitionManager _settingDefinitionManager;
    private readonly ISettingProvider _settingProvider;

    public SitesSettingDefinitionProvider_Tests()
    {
        _settingDefinitionManager = GetRequiredService<ISettingDefinitionManager>();
        _settingProvider = GetRequiredService<ISettingProvider>();
    }

    [Theory]
    [InlineData(SitesSettings.EnabledLanguages, "en")]
    [InlineData(SitesSettings.Robots.AllowIndexing, "true")]
    [InlineData(SitesSettings.Robots.AllowAiTraining, "false")]
    [InlineData(SitesSettings.Robots.AllowAiSearch, "true")]
    public async Task Should_Resolve_Platform_Default(string name, string expected)
    {
        (await _settingProvider.GetOrNullAsync(name)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(SitesSettings.EnabledLanguages)]
    [InlineData(SitesSettings.Robots.AllowIndexing)]
    [InlineData(SitesSettings.Robots.AllowAiTraining)]
    [InlineData(SitesSettings.Robots.AllowAiSearch)]
    public async Task Should_Not_Be_Locked_To_Tenant_Provider(string name)
    {
        // No .WithProviders("T"): an empty Providers list means every provider is allowed, which is what
        // lets a TenantId==null request (the host's own site) still resolve the Global value (总体设计 §4.3).
        var definition = await _settingDefinitionManager.GetOrNullAsync(name);

        definition.ShouldNotBeNull();
        definition!.Providers.ShouldBeEmpty();
    }
}
