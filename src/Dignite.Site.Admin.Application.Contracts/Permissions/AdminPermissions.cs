using Volo.Abp.Reflection;

namespace Dignite.Site.Admin.Permissions;

public class AdminPermissions
{
    public const string GroupName = "Admin";

    public static class Pages
    {
        public const string Default = GroupName + ".Pages";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static class ContentTypes
    {
        public const string Default = GroupName + ".ContentTypes";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static class Fields
    {
        public const string Default = GroupName + ".Fields";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";

        /// <summary>Separate from <see cref="Update"/> - see <c>IFieldAdminAppService.RenameAsync</c>.</summary>
        public const string Rename = Default + ".Rename";
    }

    public static class FieldGroups
    {
        public const string Default = GroupName + ".FieldGroups";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static class Contents
    {
        public const string Default = GroupName + ".Contents";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(AdminPermissions));
    }
}
