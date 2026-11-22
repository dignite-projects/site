using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class BlobManagement_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlobDirectoryDescriptors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Path = table.Column<string>(type: "TEXT", maxLength: 527, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NameKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlobDirectoryDescriptors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlobFileDescriptors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    BlobName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    NameKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DirectoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPending = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeleterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlobFileDescriptors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlobDirectoryDescriptors_NameKey",
                table: "BlobDirectoryDescriptors",
                column: "NameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlobDirectoryDescriptors_ParentId",
                table: "BlobDirectoryDescriptors",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobDirectoryDescriptors_Path",
                table: "BlobDirectoryDescriptors",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_BlobDirectoryDescriptors_TenantId_ContainerName_ParentId",
                table: "BlobDirectoryDescriptors",
                columns: new[] { "TenantId", "ContainerName", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_DirectoryId",
                table: "BlobFileDescriptors",
                column: "DirectoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_NameKey",
                table: "BlobFileDescriptors",
                column: "NameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_BlobName",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "BlobName" });

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_DirectoryId",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "DirectoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_Name",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlobDirectoryDescriptors");

            migrationBuilder.DropTable(
                name: "BlobFileDescriptors");
        }
    }
}
