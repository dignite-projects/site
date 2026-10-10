using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <summary>
    /// Data-only follow-up to <see cref="Site_AbsorbFileExplorer"/>:
    /// <list type="bullet">
    /// <item>File addresses stored before the move point at File Explorer's read endpoint
    /// (<c>/api/file-explorer/files/{container}/{blob}</c>); Site serves the same files at
    /// <c>/api/site-public/files/{container}/{blob}</c>. The path is rewritten wherever an address is
    /// stored - content field values (<c>SiteContents.FlexFields</c>: file fields, SEO share images,
    /// rich text) and the branding logo settings - keeping the host name and query (<c>__tenant</c>).</item>
    /// <item>File Explorer's own "manage every file" permission is gone - <c>SiteAdmin.Contents</c> covers
    /// it now - so its grants are deleted rather than left orphaned in permission management.</item>
    /// </list>
    /// <c>Down</c> restores the old paths only (best effort, like any such rewrite: it cannot tell rows
    /// written under the new path after this ran from rows this migration touched); deleted grants are not
    /// restored. The SQL Server equivalent is in CHANGELOG.md.
    /// </summary>
    public partial class Site_MigrateFileExplorerData : Migration
    {
        private const string OldPath = "/api/file-explorer/files/";
        private const string NewPath = "/api/site-public/files/";
        private const string ManagementPermission = "FileExplorer.File.Management";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RewritePaths(migrationBuilder, OldPath, NewPath);

            migrationBuilder.Sql(
                $"DELETE FROM \"AbpPermissionGrants\" WHERE \"Name\" = '{ManagementPermission}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RewritePaths(migrationBuilder, NewPath, OldPath);
        }

        private static void RewritePaths(MigrationBuilder migrationBuilder, string from, string to)
        {
            migrationBuilder.Sql(
                $"UPDATE \"SiteContents\" SET \"FlexFields\" = REPLACE(\"FlexFields\", '{from}', '{to}') " +
                $"WHERE \"FlexFields\" LIKE '%{from}%';");

            migrationBuilder.Sql(
                $"UPDATE \"AbpSettings\" SET \"Value\" = REPLACE(\"Value\", '{from}', '{to}') " +
                $"WHERE \"Name\" LIKE 'Site.Branding.%' AND \"Value\" LIKE '%{from}%';");
        }
    }
}
