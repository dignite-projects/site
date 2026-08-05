using Dignite.Site.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Site.Common.Permissions;

public class CommonPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(CommonPermissions.GroupName, L("Permission:Common"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SiteResource>(name);
    }
}
