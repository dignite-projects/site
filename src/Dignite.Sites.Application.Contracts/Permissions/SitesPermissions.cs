using Volo.Abp.Reflection;

namespace Dignite.Sites.Permissions;

public class SitesPermissions
{
    public const string GroupName = "Sites";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(SitesPermissions));
    }
}
