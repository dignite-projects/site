using Dignite.Site.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Site.Permissions;

public class SitePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(SitePermissions.GroupName, L("Permission:Site"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SiteResource>(name);
    }
}
