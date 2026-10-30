using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class Site_FilterUniqueIndexesBySoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SitePages_TenantId_Name",
                table: "SitePages");

            migrationBuilder.DropIndex(
                name: "IX_SitePages_TenantId_Route",
                table: "SitePages");

            migrationBuilder.DropIndex(
                name: "IX_SiteFields_TenantId_Name",
                table: "SiteFields");

            migrationBuilder.DropIndex(
                name: "IX_SiteContentTypes_TenantId_PageId_Name",
                table: "SiteContentTypes");

            migrationBuilder.DropIndex(
                name: "IX_SiteContents_TenantId_PageId_CultureName_Slug",
                table: "SiteContents");

            migrationBuilder.CreateIndex(
                name: "IX_SitePages_TenantId_Name",
                table: "SitePages",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SitePages_TenantId_Route",
                table: "SitePages",
                columns: new[] { "TenantId", "Route" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SiteFields_TenantId_Name",
                table: "SiteFields",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SiteContentTypes_TenantId_PageId_Name",
                table: "SiteContentTypes",
                columns: new[] { "TenantId", "PageId", "Name" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SiteContents_TenantId_PageId_CultureName_Slug",
                table: "SiteContents",
                columns: new[] { "TenantId", "PageId", "CultureName", "Slug" },
                unique: true,
                filter: "IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SitePages_TenantId_Name",
                table: "SitePages");

            migrationBuilder.DropIndex(
                name: "IX_SitePages_TenantId_Route",
                table: "SitePages");

            migrationBuilder.DropIndex(
                name: "IX_SiteFields_TenantId_Name",
                table: "SiteFields");

            migrationBuilder.DropIndex(
                name: "IX_SiteContentTypes_TenantId_PageId_Name",
                table: "SiteContentTypes");

            migrationBuilder.DropIndex(
                name: "IX_SiteContents_TenantId_PageId_CultureName_Slug",
                table: "SiteContents");

            migrationBuilder.CreateIndex(
                name: "IX_SitePages_TenantId_Name",
                table: "SitePages",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SitePages_TenantId_Route",
                table: "SitePages",
                columns: new[] { "TenantId", "Route" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteFields_TenantId_Name",
                table: "SiteFields",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteContentTypes_TenantId_PageId_Name",
                table: "SiteContentTypes",
                columns: new[] { "TenantId", "PageId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteContents_TenantId_PageId_CultureName_Slug",
                table: "SiteContents",
                columns: new[] { "TenantId", "PageId", "CultureName", "Slug" },
                unique: true);
        }
    }
}
