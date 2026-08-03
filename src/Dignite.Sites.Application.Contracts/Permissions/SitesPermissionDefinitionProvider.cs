using Dignite.Sites.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Sites.Permissions;

public class SitesPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(SitesPermissions.GroupName, L("Permission:Sites"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SitesResource>(name);
    }
}
