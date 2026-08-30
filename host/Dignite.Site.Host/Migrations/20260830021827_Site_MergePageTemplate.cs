using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class Site_MergePageTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill before dropping ContentTemplate or tightening Template to NOT NULL: prefer whatever
            // ContentTemplate already named (its job is what the surviving Template column takes over),
            // fall back to Template's own value, and only default to "Default" when neither was ever set -
            // the scaffolded migration's own "" default would leave existing rows with a blank Template,
            // which SiteRenderController.RenderAsync now passes straight to View() with nothing to fall
            // back on (issue #53).
            migrationBuilder.Sql(
                "UPDATE \"SitePages\" SET \"Template\" = COALESCE(NULLIF(\"ContentTemplate\", ''), NULLIF(\"Template\", ''), 'Default') " +
                "WHERE \"Template\" IS NULL OR \"Template\" = '';");

            migrationBuilder.DropColumn(
                name: "ContentTemplate",
                table: "SitePages");

            migrationBuilder.AlterColumn<string>(
                name: "Template",
                table: "SitePages",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "Default",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 256,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Template",
                table: "SitePages",
                type: "TEXT",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<string>(
                name: "ContentTemplate",
                table: "SitePages",
                type: "TEXT",
                maxLength: 256,
                nullable: true);
        }
    }
}
