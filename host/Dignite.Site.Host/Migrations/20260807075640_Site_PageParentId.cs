using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class Site_PageParentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "SitePages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SitePages_ParentId",
                table: "SitePages",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_SitePages_TenantId_ParentId",
                table: "SitePages",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SitePages_SitePages_ParentId",
                table: "SitePages",
                column: "ParentId",
                principalTable: "SitePages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SitePages_SitePages_ParentId",
                table: "SitePages");

            migrationBuilder.DropIndex(
                name: "IX_SitePages_ParentId",
                table: "SitePages");

            migrationBuilder.DropIndex(
                name: "IX_SitePages_TenantId_ParentId",
                table: "SitePages");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "SitePages");
        }
    }
}
