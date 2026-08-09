using Dignite.Site.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Site.Admin.Permissions;

public class AdminPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(AdminPermissions.GroupName, L("Permission:Admin"));

        var pages = myGroup.AddPermission(AdminPermissions.Pages.Default, L("Permission:Pages"));
        pages.AddChild(AdminPermissions.Pages.Create, L("Permission:Create"));
        pages.AddChild(AdminPermissions.Pages.Update, L("Permission:Update"));
        pages.AddChild(AdminPermissions.Pages.Delete, L("Permission:Delete"));

        var contentTypes = myGroup.AddPermission(AdminPermissions.ContentTypes.Default, L("Permission:ContentTypes"));
        contentTypes.AddChild(AdminPermissions.ContentTypes.Create, L("Permission:Create"));
        contentTypes.AddChild(AdminPermissions.ContentTypes.Update, L("Permission:Update"));
        contentTypes.AddChild(AdminPermissions.ContentTypes.Delete, L("Permission:Delete"));

        var fields = myGroup.AddPermission(AdminPermissions.Fields.Default, L("Permission:Fields"));
        fields.AddChild(AdminPermissions.Fields.Create, L("Permission:Create"));
        fields.AddChild(AdminPermissions.Fields.Update, L("Permission:Update"));
        fields.AddChild(AdminPermissions.Fields.Delete, L("Permission:Delete"));
        fields.AddChild(AdminPermissions.Fields.Rename, L("Permission:Fields.Rename"));


        var contents = myGroup.AddPermission(AdminPermissions.Contents.Default, L("Permission:Contents"));
        contents.AddChild(AdminPermissions.Contents.Create, L("Permission:Create"));
        contents.AddChild(AdminPermissions.Contents.Update, L("Permission:Update"));
        contents.AddChild(AdminPermissions.Contents.Delete, L("Permission:Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SiteResource>(name);
    }
}
