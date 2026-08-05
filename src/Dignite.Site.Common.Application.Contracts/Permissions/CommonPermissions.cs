using Volo.Abp.Reflection;

namespace Dignite.Site.Common.Permissions;

public class CommonPermissions
{
    public const string GroupName = "Common";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(CommonPermissions));
    }
}
