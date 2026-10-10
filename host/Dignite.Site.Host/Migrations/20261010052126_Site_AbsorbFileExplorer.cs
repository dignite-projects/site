using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <summary>
    /// The file library moves from Dignite.FileExplorer's tables into Site's own: <c>FeDirectoryDescriptors</c>
    /// and <c>FeFileDescriptors</c> are <b>renamed</b> to <c>SiteDirectoryDescriptors</c> and
    /// <c>SiteFileDescriptors</c> - never dropped and recreated - so every row, id and blob name survives,
    /// and with them every file address already stored in content.
    /// <para>
    /// Along the way: <c>Md5</c> (which always held a SHA-256) becomes <c>Hash</c>; <c>CellName</c> and
    /// <c>EntityId</c>, which only File Explorer's file grid and entity-authorization handlers used, are
    /// dropped; the primary keys, foreign keys (the directory self-reference included) and indexes take
    /// the new table names; and the unique hash indexes now cover live rows only (<c>IsDeleted = 0</c>), so
    /// a deleted file no longer blocks the same bytes from being uploaded again.
    /// </para>
    /// <para>
    /// SQLite has no ALTER for keys, foreign keys or dropped columns: EF Core rebuilds both tables from this
    /// migration's target model and copies the rows across. The equivalent SQL Server script (sp_rename
    /// based) is in CHANGELOG.md.
    /// </para>
    /// </summary>
    public partial class Site_AbsorbFileExplorer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeFileDescriptors_FeDirectoryDescriptors_DirectoryId",
                table: "FeFileDescriptors");

            migrationBuilder.DropForeignKey(
                name: "FK_FeDirectoryDescriptors_FeDirectoryDescriptors_ParentId",
                table: "FeDirectoryDescriptors");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeFileDescriptors",
                table: "FeFileDescriptors");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeDirectoryDescriptors",
                table: "FeDirectoryDescriptors");

            // Indexes on columns that go away or change meaning; the hash ones are recreated below.
            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_EntityId",
                table: "FeFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_ContainerName_Md5",
                table: "FeFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_Md5",
                table: "FeFileDescriptors");

            migrationBuilder.RenameTable(
                name: "FeDirectoryDescriptors",
                newName: "SiteDirectoryDescriptors");

            migrationBuilder.RenameTable(
                name: "FeFileDescriptors",
                newName: "SiteFileDescriptors");

            migrationBuilder.DropColumn(
                name: "CellName",
                table: "SiteFileDescriptors");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "SiteFileDescriptors");

            migrationBuilder.RenameColumn(
                name: "Md5",
                table: "SiteFileDescriptors",
                newName: "Hash");

            migrationBuilder.RenameIndex(
                name: "IX_FeDirectoryDescriptors_ParentId",
                table: "SiteDirectoryDescriptors",
                newName: "IX_SiteDirectoryDescriptors_ParentId");

            migrationBuilder.RenameIndex(
                name: "IX_FeDirectoryDescriptors_TenantId_ContainerName_CreatorId_ParentId",
                table: "SiteDirectoryDescriptors",
                newName: "IX_SiteDirectoryDescriptors_TenantId_ContainerName_CreatorId_ParentId");

            migrationBuilder.RenameIndex(
                name: "IX_FeFileDescriptors_ContainerName_BlobName",
                table: "SiteFileDescriptors",
                newName: "IX_SiteFileDescriptors_ContainerName_BlobName");

            migrationBuilder.RenameIndex(
                name: "IX_FeFileDescriptors_DirectoryId",
                table: "SiteFileDescriptors",
                newName: "IX_SiteFileDescriptors_DirectoryId");

            migrationBuilder.RenameIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_BlobName",
                table: "SiteFileDescriptors",
                newName: "IX_SiteFileDescriptors_TenantId_ContainerName_BlobName");

            migrationBuilder.RenameIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_CreationTime_CreatorId_DirectoryId",
                table: "SiteFileDescriptors",
                newName: "IX_SiteFileDescriptors_TenantId_ContainerName_CreationTime_CreatorId_DirectoryId");

            migrationBuilder.RenameIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_ReferBlobName",
                table: "SiteFileDescriptors",
                newName: "IX_SiteFileDescriptors_TenantId_ContainerName_ReferBlobName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SiteDirectoryDescriptors",
                table: "SiteDirectoryDescriptors",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SiteFileDescriptors",
                table: "SiteFileDescriptors",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SiteDirectoryDescriptors_SiteDirectoryDescriptors_ParentId",
                table: "SiteDirectoryDescriptors",
                column: "ParentId",
                principalTable: "SiteDirectoryDescriptors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SiteFileDescriptors_SiteDirectoryDescriptors_DirectoryId",
                table: "SiteFileDescriptors",
                column: "DirectoryId",
                principalTable: "SiteDirectoryDescriptors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_SiteFileDescriptors_ContainerName_Hash",
                table: "SiteFileDescriptors",
                columns: new[] { "ContainerName", "Hash" },
                unique: true,
                filter: "TenantId IS NULL AND Hash <> '' AND IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SiteFileDescriptors_TenantId_ContainerName_Hash",
                table: "SiteFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Hash" },
                unique: true,
                filter: "TenantId IS NOT NULL AND Hash <> '' AND IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SiteFileDescriptors_SiteDirectoryDescriptors_DirectoryId",
                table: "SiteFileDescriptors");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteDirectoryDescriptors_SiteDirectoryDescriptors_ParentId",
                table: "SiteDirectoryDescriptors");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SiteFileDescriptors",
                table: "SiteFileDescriptors");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SiteDirectoryDescriptors",
                table: "SiteDirectoryDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_SiteFileDescriptors_ContainerName_Hash",
                table: "SiteFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_SiteFileDescriptors_TenantId_ContainerName_Hash",
                table: "SiteFileDescriptors");

            migrationBuilder.RenameTable(
                name: "SiteDirectoryDescriptors",
                newName: "FeDirectoryDescriptors");

            migrationBuilder.RenameTable(
                name: "SiteFileDescriptors",
                newName: "FeFileDescriptors");

            migrationBuilder.RenameColumn(
                name: "Hash",
                table: "FeFileDescriptors",
                newName: "Md5");

            migrationBuilder.AddColumn<string>(
                name: "CellName",
                table: "FeFileDescriptors",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "FeFileDescriptors",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.RenameIndex(
                name: "IX_SiteDirectoryDescriptors_ParentId",
                table: "FeDirectoryDescriptors",
                newName: "IX_FeDirectoryDescriptors_ParentId");

            migrationBuilder.RenameIndex(
                name: "IX_SiteDirectoryDescriptors_TenantId_ContainerName_CreatorId_ParentId",
                table: "FeDirectoryDescriptors",
                newName: "IX_FeDirectoryDescriptors_TenantId_ContainerName_CreatorId_ParentId");

            migrationBuilder.RenameIndex(
                name: "IX_SiteFileDescriptors_ContainerName_BlobName",
                table: "FeFileDescriptors",
                newName: "IX_FeFileDescriptors_ContainerName_BlobName");

            migrationBuilder.RenameIndex(
                name: "IX_SiteFileDescriptors_DirectoryId",
                table: "FeFileDescriptors",
                newName: "IX_FeFileDescriptors_DirectoryId");

            migrationBuilder.RenameIndex(
                name: "IX_SiteFileDescriptors_TenantId_ContainerName_BlobName",
                table: "FeFileDescriptors",
                newName: "IX_FeFileDescriptors_TenantId_ContainerName_BlobName");

            migrationBuilder.RenameIndex(
                name: "IX_SiteFileDescriptors_TenantId_ContainerName_CreationTime_CreatorId_DirectoryId",
                table: "FeFileDescriptors",
                newName: "IX_FeFileDescriptors_TenantId_ContainerName_CreationTime_CreatorId_DirectoryId");

            migrationBuilder.RenameIndex(
                name: "IX_SiteFileDescriptors_TenantId_ContainerName_ReferBlobName",
                table: "FeFileDescriptors",
                newName: "IX_FeFileDescriptors_TenantId_ContainerName_ReferBlobName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeDirectoryDescriptors",
                table: "FeDirectoryDescriptors",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeFileDescriptors",
                table: "FeFileDescriptors",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FeDirectoryDescriptors_FeDirectoryDescriptors_ParentId",
                table: "FeDirectoryDescriptors",
                column: "ParentId",
                principalTable: "FeDirectoryDescriptors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeFileDescriptors_FeDirectoryDescriptors_DirectoryId",
                table: "FeFileDescriptors",
                column: "DirectoryId",
                principalTable: "FeDirectoryDescriptors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_ContainerName_Md5",
                table: "FeFileDescriptors",
                columns: new[] { "ContainerName", "Md5" },
                unique: true,
                filter: "TenantId IS NULL AND Md5 <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_EntityId",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_Md5",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Md5" },
                unique: true,
                filter: "TenantId IS NOT NULL AND Md5 <> ''");
        }
    }
}
