using Volo.Abp.Reflection;

namespace Dignite.Site.Common.Permissions;

public class SiteCommonPermissions
{
    public const string GroupName = "SiteCommon";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(SiteCommonPermissions));
    }
}
