using Dignite.Site.Host.Permissions;
using Dignite.Site.Host.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity.Web.Navigation;
using Volo.Abp.SettingManagement.Web.Navigation;
using Volo.Abp.UI.Navigation;
using Volo.Abp.Identity.Web.Navigation;
using Volo.Abp.TenantManagement.Web.Navigation;
namespace Dignite.Site.Host.Menus;
public class HostMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }
    private static Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<HostResource>();
        context.Menu.Items.Insert(
            0,
            new ApplicationMenuItem(
                HostMenus.Home,
                l["Menu:Home"],
                "~/",
                icon: "fas fa-home",
                order: 0
            )
        );
        //Administration
        var administration = context.Menu.GetAdministration();
        administration.Order = 5;
        //Administration->Tenant Management
        administration.SetSubItemOrder(TenantManagementMenuNames.GroupName, 2);
        //Administration->Identity
        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);
        //Administration->Settings
        administration.SetSubItemOrder(SettingManagementMenuNames.GroupName, 8);

        return Task.CompletedTask;
    }
}
