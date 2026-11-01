using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class Site_Themes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteTemplateVersions");

            migrationBuilder.DropTable(
                name: "SiteTemplates");

            migrationBuilder.CreateTable(
                name: "SiteThemeBlobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteThemeBlobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteThemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DefaultLayout = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LiveReleaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteThemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThemeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultLayout = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ChangedFileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteReleases_SiteThemes_ThemeId",
                        column: x => x.ThemeId,
                        principalTable: "SiteThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteThemeFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThemeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Path = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteThemeFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteThemeFiles_SiteThemes_ThemeId",
                        column: x => x.ThemeId,
                        principalTable: "SiteThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteReleaseFiles",
                columns: table => new
                {
                    ReleaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteReleaseFiles", x => new { x.ReleaseId, x.Path });
                    table.ForeignKey(
                        name: "FK_SiteReleaseFiles_SiteReleases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "SiteReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteReleases_ThemeId_Number",
                table: "SiteReleases",
                columns: new[] { "ThemeId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteThemeBlobs_TenantId_ContentHash",
                table: "SiteThemeBlobs",
                columns: new[] { "TenantId", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteThemeFiles_ThemeId_Path",
                table: "SiteThemeFiles",
                columns: new[] { "ThemeId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteThemes_TenantId",
                table: "SiteThemes",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteReleaseFiles");

            migrationBuilder.DropTable(
                name: "SiteThemeBlobs");

            migrationBuilder.DropTable(
                name: "SiteThemeFiles");

            migrationBuilder.DropTable(
                name: "SiteReleases");

            migrationBuilder.DropTable(
                name: "SiteThemes");

            migrationBuilder.CreateTable(
                name: "SiteTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeleterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PublishedVersionNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteTemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteTemplateVersions_SiteTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "SiteTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteTemplates_TenantId_Name",
                table: "SiteTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SiteTemplateVersions_TemplateId_Number",
                table: "SiteTemplateVersions",
                columns: new[] { "TemplateId", "Number" },
                unique: true);
        }
    }
}
