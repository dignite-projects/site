using Dignite.Sites.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Sites.Admin.Permissions;

public class AdminPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(AdminPermissions.GroupName, L("Permission:Admin"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SitesResource>(name);
    }
}
