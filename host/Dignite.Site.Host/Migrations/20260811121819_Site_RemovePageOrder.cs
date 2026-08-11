using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class Site_RemovePageOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Order",
                table: "SitePages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "SitePages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
