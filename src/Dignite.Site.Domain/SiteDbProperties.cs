namespace Dignite.Site;

public static class SiteDbProperties
{
    public static string DbTablePrefix { get; set; } = "Site";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "Site";
}
