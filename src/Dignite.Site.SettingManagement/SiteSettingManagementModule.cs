using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;

namespace Dignite.Site;

/// <summary>
/// Lets Site write its settings through Setting Management (<see cref="Settings.ISiteSettingWriter"/>). Loaded by the host
/// that wants a site's languages, or any other setting, set from the application; it brings the Setting Management domain
/// module with it, whose database the host has to provide.
/// </summary>
[DependsOn(
    typeof(SiteDomainModule),
    typeof(AbpSettingManagementDomainModule)
)]
public class SiteSettingManagementModule : AbpModule
{
}
