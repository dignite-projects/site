using Dignite.Site.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Site.Public.Permissions;

public class PublicPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(PublicPermissions.GroupName, L("Permission:Public"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SiteResource>(name);
    }
}
