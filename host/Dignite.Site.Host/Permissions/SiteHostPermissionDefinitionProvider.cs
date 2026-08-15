using Dignite.Site.Host.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Host.Permissions;

public class SiteHostPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(SiteHostPermissions.GroupName);


        
        //Define your own permissions here. Example:
        //myGroup.AddPermission(SiteHostPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SiteHostResource>(name);
    }
}
