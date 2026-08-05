using Microsoft.Extensions.Localization;
using Dignite.Site.Host.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace Dignite.Site.Host;

[Dependency(ReplaceServices = true)]
public class HostBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<HostResource> _localizer;

    public HostBrandingProvider(IStringLocalizer<HostResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
