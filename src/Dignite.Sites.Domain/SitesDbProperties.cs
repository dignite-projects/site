namespace Dignite.Sites;

public static class SitesDbProperties
{
    public static string DbTablePrefix { get; set; } = "Site";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "Sites";
}
