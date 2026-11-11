using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class Drive_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriveFileDescriptors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    BlobName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    DedupKey = table.Column<string>(type: "TEXT", maxLength: 162, nullable: true),
                    NameKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_DriveFileDescriptors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriveFileDescriptors_DedupKey",
                table: "DriveFileDescriptors",
                column: "DedupKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriveFileDescriptors_NameKey",
                table: "DriveFileDescriptors",
                column: "NameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriveFileDescriptors_TenantId_ContainerName_BlobName",
                table: "DriveFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "BlobName" });

            migrationBuilder.CreateIndex(
                name: "IX_DriveFileDescriptors_TenantId_ContainerName_Hash",
                table: "DriveFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Hash" });

            migrationBuilder.CreateIndex(
                name: "IX_DriveFileDescriptors_TenantId_ContainerName_Name",
                table: "DriveFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriveFileDescriptors");
        }
    }
}
