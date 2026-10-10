using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <summary>
    /// Data-only: Site file addresses are now stored relative (<c>/api/site-public/files/...</c>, see
    /// <c>SiteFileUrl.Build</c>), so the absolute ones already stored -
    /// <c>{scheme}://{host}/api/site-public/files/...</c>, as the admin API and the MCP tools handed them out
    /// until now - lose their scheme and host, in <c>SiteContents.FlexFields</c> and in the branding logo
    /// settings (<c>Site.Branding.%</c>). Both spellings are rewritten: the plain one, and the JSON-escaped
    /// one (<c>https:\/\/host\/api\/site-public\/files\/</c>) a serializer may write into <c>FlexFields</c>.
    /// <para>
    /// SQLite has no regular expressions, so the hosts are listed rather than matched: <see cref="Hosts"/> is
    /// the set this host's own database could hold, taken when this migration was written - the development
    /// database had no absolute address stored at the time (<c>LIKE '%://%/api/site-public/files/%'</c>
    /// matched no row, escaped spelling included), so the set is the origin this Host serves on and hands
    /// addresses out from (<c>App:SelfUrl</c>, <c>launchSettings.json</c>). Any other environment runs the
    /// script in CHANGELOG.md with its own hosts instead.
    /// </para>
    /// <para>
    /// <c>Down</c> does nothing: which host an address was stored with is gone once it is relative, and a
    /// relative address keeps working on the earlier version too, since the public site resolves it against
    /// its own origin.
    /// </para>
    /// </summary>
    public partial class Site_RelativeFileUrls : Migration
    {
        private static readonly string[] Hosts =
        {
            "https://localhost:44315"
        };

        private const string Path = "/api/site-public/files/";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var host in Hosts)
            {
                RewritePaths(migrationBuilder, host + Path, Path);
                RewritePaths(migrationBuilder, Escape(host + Path), Escape(Path));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }

        /// <summary>The JSON-escaped spelling of <paramref name="value"/>: every <c>/</c> as <c>\/</c>.</summary>
        private static string Escape(string value)
        {
            return value.Replace("/", @"\/");
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
