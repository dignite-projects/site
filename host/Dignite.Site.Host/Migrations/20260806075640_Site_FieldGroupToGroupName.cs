using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class Site_FieldGroupToGroupName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add and backfill before dropping anything: the scaffolded order dropped the group table
            // and the foreign key first, which would have thrown away every existing field's grouping.
            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                table: "SiteFields",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                @"UPDATE SiteFields
                     SET GroupName = (SELECT g.Name FROM SiteFieldGroups g WHERE g.Id = SiteFields.GroupId)
                   WHERE GroupId IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteFields_SiteFieldGroups_GroupId",
                table: "SiteFields");

            migrationBuilder.DropIndex(
                name: "IX_SiteFields_GroupId",
                table: "SiteFields");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "SiteFields");

            migrationBuilder.DropTable(
                name: "SiteFieldGroups");

            migrationBuilder.CreateIndex(
                name: "IX_SiteFields_TenantId_GroupName",
                table: "SiteFields",
                columns: new[] { "TenantId", "GroupName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SiteFields_TenantId_GroupName",
                table: "SiteFields");

            migrationBuilder.DropColumn(
                name: "GroupName",
                table: "SiteFields");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "SiteFields",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SiteFieldGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteFieldGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteFields_GroupId",
                table: "SiteFields",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteFieldGroups_TenantId_Name",
                table: "SiteFieldGroups",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SiteFields_SiteFieldGroups_GroupId",
                table: "SiteFields",
                column: "GroupId",
                principalTable: "SiteFieldGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
