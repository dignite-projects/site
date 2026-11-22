using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class Site_RemoveLegacyFileLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteFileDescriptors");

            migrationBuilder.DropTable(
                name: "SiteDirectoryDescriptors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteDirectoryDescriptors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteDirectoryDescriptors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteDirectoryDescriptors_SiteDirectoryDescriptors_ParentId",
                        column: x => x.ParentId,
                        principalTable: "SiteDirectoryDescriptors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteFileDescriptors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BlobName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeleterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DirectoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ReferBlobName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteFileDescriptors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteFileDescriptors_SiteDirectoryDescriptors_DirectoryId",
                        column: x => x.DirectoryId,
                        principalTable: "SiteDirectoryDescriptors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteDirectoryDescriptors_ParentId",
                table: "SiteDirectoryDescriptors",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteDirectoryDescriptors_TenantId_ContainerName_CreatorId_ParentId",
                table: "SiteDirectoryDescriptors",
                columns: new[] { "TenantId", "ContainerName", "CreatorId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteFileDescriptors_ContainerName_BlobName",
                table: "SiteFileDescriptors",
                columns: new[] { "ContainerName", "BlobName" },
                unique: true,
                filter: "TenantId IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SiteFileDescriptors_ContainerName_Hash",
                table: "SiteFileDescriptors",
                columns: new[] { "ContainerName", "Hash" },
                unique: true,
                filter: "TenantId IS NULL AND Hash <> '' AND IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SiteFileDescriptors_DirectoryId",
                table: "SiteFileDescriptors",
                column: "DirectoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteFileDescriptors_TenantId_ContainerName_BlobName",
                table: "SiteFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "BlobName" },
                unique: true,
                filter: "TenantId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SiteFileDescriptors_TenantId_ContainerName_CreationTime_CreatorId_DirectoryId",
                table: "SiteFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "CreationTime", "CreatorId", "DirectoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteFileDescriptors_TenantId_ContainerName_Hash",
                table: "SiteFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Hash" },
                unique: true,
                filter: "TenantId IS NOT NULL AND Hash <> '' AND IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SiteFileDescriptors_TenantId_ContainerName_ReferBlobName",
                table: "SiteFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "ReferBlobName" });
        }
    }
}
