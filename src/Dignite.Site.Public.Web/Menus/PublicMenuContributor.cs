using System.Threading.Tasks;
using Volo.Abp.UI.Navigation;

namespace Dignite.Site.Public.Menus;

public class PublicMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        //Add main menu items.
        context.Menu.AddItem(new ApplicationMenuItem(PublicMenus.Prefix, displayName: "Site", "~/Site", icon: "fa fa-globe"));

        return Task.CompletedTask;
    }
}
