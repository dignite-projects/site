using Volo.Abp.Reflection;

namespace Dignite.Site.Permissions;

public class SitePermissions
{
    public const string GroupName = "Site";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(SitePermissions));
    }
}
