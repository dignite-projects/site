using Volo.Abp.Reflection;

namespace Dignite.Site.Public.Permissions;

public class SitePublicPermissions
{
    public const string GroupName = "SitePublic";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(SitePublicPermissions));
    }
}
