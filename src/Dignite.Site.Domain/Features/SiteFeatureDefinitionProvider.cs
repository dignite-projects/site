using Dignite.Site.Localization;
using Volo.Abp.Features;
using Volo.Abp.Localization;
using Volo.Abp.Validation.StringValues;

namespace Dignite.Site.Features;

public class SiteFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        var group = context.AddGroup(SiteFeatures.GroupName, L("Feature:Site"));

        // No allowedProviders, same reasoning as the settings: an empty list allows every provider, so a
        // tenant value, an edition value and a test override all take effect.
        group.AddFeature(
            SiteFeatures.Enable,
            defaultValue: "true",
            displayName: L("Feature:Site.Enable"),
            description: L("Feature:Site.Enable:Description"),
            valueType: new ToggleStringValueType(),
            // Visible to clients so the Angular admin can hide its menu from application-configuration -
            // a convenience only, the Admin application services are what actually enforce it.
            isVisibleToClients: true);
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SiteResource>(name);
    }
}
