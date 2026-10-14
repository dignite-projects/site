using Dignite.Site.Features;
using Dignite.Site.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Features;
using Volo.Abp.Localization;

namespace Dignite.Site.Admin.Permissions;

public class SiteAdminPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(SiteAdminPermissions.GroupName, L("Permission:SiteAdmin"));

        var pages = myGroup.AddPermission(SiteAdminPermissions.Pages.Default, L("Permission:Pages"));
        pages.AddChild(SiteAdminPermissions.Pages.Create, L("Permission:Create"));
        pages.AddChild(SiteAdminPermissions.Pages.Update, L("Permission:Update"));
        pages.AddChild(SiteAdminPermissions.Pages.Delete, L("Permission:Delete"));

        var contentTypes = myGroup.AddPermission(SiteAdminPermissions.ContentTypes.Default, L("Permission:ContentTypes"));
        contentTypes.AddChild(SiteAdminPermissions.ContentTypes.Create, L("Permission:Create"));
        contentTypes.AddChild(SiteAdminPermissions.ContentTypes.Update, L("Permission:Update"));
        contentTypes.AddChild(SiteAdminPermissions.ContentTypes.Delete, L("Permission:Delete"));

        var fields = myGroup.AddPermission(SiteAdminPermissions.Fields.Default, L("Permission:Fields"));
        fields.AddChild(SiteAdminPermissions.Fields.Create, L("Permission:Create"));
        fields.AddChild(SiteAdminPermissions.Fields.Update, L("Permission:Update"));
        fields.AddChild(SiteAdminPermissions.Fields.Delete, L("Permission:Delete"));
        fields.AddChild(SiteAdminPermissions.Fields.Rename, L("Permission:Fields.Rename"));


        var contents = myGroup.AddPermission(SiteAdminPermissions.Contents.Default, L("Permission:Contents"));
        contents.AddChild(SiteAdminPermissions.Contents.Create, L("Permission:Create"));
        contents.AddChild(SiteAdminPermissions.Contents.Update, L("Permission:Update"));
        contents.AddChild(SiteAdminPermissions.Contents.Delete, L("Permission:Delete"));

        // With Site.Enable off these permissions read as not granted, which is what takes the admin menu
        // away: the Angular routes hide on requiredPolicy, and application-configuration reports what the
        // checker says. Every permission needs it, children included - ABP checks each one on its own, a
        // parent's state checker is not inherited. Done over the whole group so a permission added later is
        // covered without having to remember it.
        foreach (var permission in myGroup.GetPermissionsWithChildren())
        {
            permission.RequireFeatures(SiteFeatures.Enable);
        }
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SiteResource>(name);
    }
}
