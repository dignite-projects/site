namespace Dignite.Sites.Settings;

public static class SitesSettings
{
    public const string GroupName = "Sites";

    public const string EnabledLanguages = GroupName + ".EnabledLanguages";

    public static class Robots
    {
        public const string AllowIndexing = GroupName + ".Robots.AllowIndexing";
        public const string AllowAiTraining = GroupName + ".Robots.AllowAiTraining";
        public const string AllowAiSearch = GroupName + ".Robots.AllowAiSearch";
    }
}
