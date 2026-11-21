namespace Dignite.Site.BlobManagementMigration;

/// <summary>
/// Options of the migration of Site's legacy files to Blob Management. Bound from the <c>SiteBlobManagementMigration</c> configuration
/// section; a module's <c>Configure&lt;SiteBlobManagementMigrationOptions&gt;</c> overrides it.
/// </summary>
public class SiteBlobManagementMigrationOptions
{
    public const string ConfigurationSection = "SiteBlobManagementMigration";

    /// <summary>
    /// <c>true</c>, the default: the migration only reports what it would do - in the log, one report per tenant - and
    /// writes nothing. Set it to <c>false</c> to migrate.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
