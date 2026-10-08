using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Features;
using Xunit;

namespace Dignite.Site.Features;

public class SiteFeatureDefinitionProvider_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly IFeatureDefinitionManager _featureDefinitionManager;
    private readonly IFeatureChecker _featureChecker;
    private readonly TestFeatureValueProvider _testFeatures;

    public SiteFeatureDefinitionProvider_Tests()
    {
        _featureDefinitionManager = GetRequiredService<IFeatureDefinitionManager>();
        _featureChecker = GetRequiredService<IFeatureChecker>();
        _testFeatures = GetRequiredService<TestFeatureValueProvider>();
    }

    [Fact]
    public async Task Should_Be_Enabled_By_Default()
    {
        // A host without the Feature Management module has no way to switch this on, so "off unless
        // granted" would lock it out of its own admin surface.
        (await _featureChecker.IsEnabledAsync(SiteFeatures.Enable)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Be_Visible_To_Clients_And_Not_Locked_To_A_Provider()
    {
        var definition = await _featureDefinitionManager.GetOrNullAsync(SiteFeatures.Enable);

        definition.ShouldNotBeNull();
        definition!.DefaultValue.ShouldBe("true");
        // The Angular admin reads it from application-configuration to hide its menu.
        definition.IsVisibleToClients.ShouldBeTrue();
        // An empty list allows every provider: tenant and edition values (and the test override) all apply.
        definition.AllowedProviders.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Honour_A_Value_From_A_Higher_Provider()
    {
        _testFeatures.Set(SiteFeatures.Enable, "false");

        (await _featureChecker.IsEnabledAsync(SiteFeatures.Enable)).ShouldBeFalse();
    }
}
