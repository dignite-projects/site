using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <summary>
    /// Data-only follow-up to <see cref="Site_MigrateFileExplorerData"/>, which missed one stored form of the
    /// same addresses:
    /// <list type="bullet">
    /// <item><c>FlexFields</c> is JSON, and the serializer may escape <c>/</c> as <c>\/</c>, so some values
    /// hold <c>http:\/\/host\/api\/file-explorer\/files\/{container}\/{blob}</c>. The plain
    /// <c>REPLACE</c> in <see cref="Site_MigrateFileExplorerData"/> cannot match that spelling, and those
    /// addresses (typically on home and list pages) kept pointing at the removed File Explorer route.
    /// This migration rewrites the escaped spelling to <c>\/api\/site-public\/files\/</c>, in
    /// <c>SiteContents.FlexFields</c> and in the branding logo settings, like its predecessor.</item>
    /// </list>
    /// It is a separate migration because <see cref="Site_MigrateFileExplorerData"/> already shipped with
    /// 0.1.0-preview.25; a migration that has been applied is not run again, so editing it would not reach
    /// databases that already ran it. <c>Down</c> restores the old escaped paths only (best effort, like any
    /// such rewrite). The SQL Server equivalent is in CHANGELOG.md.
    /// </summary>
    public partial class Site_MigrateFileExplorerEscapedPaths : Migration
    {
        private const string OldPathEscaped = @"\/api\/file-explorer\/files\/";
        private const string NewPathEscaped = @"\/api\/site-public\/files\/";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RewritePaths(migrationBuilder, OldPathEscaped, NewPathEscaped);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RewritePaths(migrationBuilder, NewPathEscaped, OldPathEscaped);
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
