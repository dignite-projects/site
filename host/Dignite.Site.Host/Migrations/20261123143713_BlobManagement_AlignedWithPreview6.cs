using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class BlobManagement_AlignedWithPreview6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A file marked deleted by Blob Management 10.7.7-preview.3 or earlier has lost its bytes already, but from
            // preview.6 a soft-deleted file keeps them and can be restored. Left in place, these rows would come back as
            // restorable files without bytes, so they go first. (Soft-deleted directories hold nothing and stay.)
            migrationBuilder.Sql("DELETE FROM BlobFileDescriptors WHERE IsDeleted = 1;");

            migrationBuilder.DropIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_DirectoryId",
                table: "BlobFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_Name",
                table: "BlobFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_BlobDirectoryDescriptors_NameKey",
                table: "BlobDirectoryDescriptors");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "BlobDirectoryDescriptors");

            migrationBuilder.AlterColumn<string>(
                name: "Hash",
                table: "BlobFileDescriptors",
                type: "TEXT",
                maxLength: 44,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_DirectoryId_Name",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "DirectoryId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_IsPending_CreationTime",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "IsPending", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_DirectoryId_Name",
                table: "BlobFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_BlobFileDescriptors_TenantId_IsPending_CreationTime",
                table: "BlobFileDescriptors");

            migrationBuilder.AlterColumn<string>(
                name: "Hash",
                table: "BlobFileDescriptors",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 44,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "BlobDirectoryDescriptors",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_DirectoryId",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "DirectoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_Name",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_BlobDirectoryDescriptors_NameKey",
                table: "BlobDirectoryDescriptors",
                column: "NameKey",
                unique: true);
        }
    }
}
