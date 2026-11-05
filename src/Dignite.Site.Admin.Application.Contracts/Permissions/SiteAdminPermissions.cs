using Volo.Abp.Reflection;

namespace Dignite.Site.Admin.Permissions;

public class SiteAdminPermissions
{
    public const string GroupName = "SiteAdmin";

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

    public static class Contents
    {
        public const string Default = GroupName + ".Contents";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static class Themes
    {
        /// <summary>Reading the working copy, the releases and the check, and previewing.</summary>
        public const string Default = GroupName + ".Themes";

        /// <summary>Changing the working copy: saving, renaming and deleting files, the default layout, restoring a release into it.</summary>
        public const string Edit = Default + ".Edit";

        /// <summary>Changing what the site renders: publishing a release, and making an older one live again.</summary>
        public const string Publish = Default + ".Publish";
    }

    public static class Languages
    {
        /// <summary>Reading the languages the site serves.</summary>
        public const string Default = GroupName + ".Languages";

        /// <summary>Setting them. Moves the address of pages: the default language is the one without a prefix.</summary>
        public const string Update = Default + ".Update";
    }

    public static class Seo
    {
        /// <summary>Reading the site's SEO settings: the default share image.</summary>
        public const string Default = GroupName + ".Seo";

        /// <summary>Setting them.</summary>
        public const string Update = Default + ".Update";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(SiteAdminPermissions));
    }
}
