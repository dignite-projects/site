namespace Dignite.Site.DriveMigration;

/// <summary>
/// Options of the migration of Site's legacy files to the Drive. Bound from the <c>SiteDriveMigration</c> configuration
/// section; a module's <c>Configure&lt;SiteDriveMigrationOptions&gt;</c> overrides it.
/// </summary>
public class SiteDriveMigrationOptions
{
    public const string ConfigurationSection = "SiteDriveMigration";

    /// <summary>
    /// <c>true</c>, the default: the migration only reports what it would do - in the log, one report per tenant - and
    /// writes nothing. Set it to <c>false</c> to migrate.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
