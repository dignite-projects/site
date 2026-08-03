using System.Threading.Tasks;
using Volo.Abp.UI.Navigation;

namespace Dignite.Sites.Public.Menus;

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
        context.Menu.AddItem(new ApplicationMenuItem(PublicMenus.Prefix, displayName: "Sites", "~/Sites", icon: "fa fa-globe"));

        return Task.CompletedTask;
    }
}
