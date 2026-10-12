using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <summary>
    /// Data-only: Site's two file containers are renamed, <c>site-images</c> to <c>images</c> and
    /// <c>site-files</c> to <c>attachments</c> (<c>SiteFileContainerNames.Images</c> /
    /// <c>.Attachments</c>, the latter formerly <c>.Default</c>). The name lives in three kinds of stored data,
    /// all rewritten here:
    /// <list type="bullet">
    /// <item>The container of every file and directory: <c>SiteFileDescriptors.ContainerName</c> and
    /// <c>SiteDirectoryDescriptors.ContainerName</c>.</item>
    /// <item>Every stored file address (<c>/api/site-public/files/{container}/{blob}</c>): the container
    /// segment changes, the prefix does not. In <c>SiteContents.FlexFields</c> (file fields, SEO share images,
    /// rich text) and the branding logo settings (<c>Site.Branding.%</c>), both the plain spelling and the
    /// JSON-escaped one (<c>\/api\/site-public\/files\/...</c>) a serializer may write into
    /// <c>FlexFields</c>.</item>
    /// <item>Field definitions: a file field's <c>FileContainerName</c> and a CKEditor field's
    /// <c>ImagesContainerName</c> sit in the field's configuration JSON, <c>SiteFields.Configuration</c>. The
    /// name is replaced with its double quotes, so a longer name that merely contains it cannot match.</item>
    /// </list>
    /// <para>
    /// The blobs themselves are not moved by this migration. With the filesystem provider the host renames the
    /// folders <c>{BasePath}/host/{old}</c> and <c>{BasePath}/tenants/{tenantId}/{old}</c>; with the database
    /// provider it renames <c>AbpBlobContainers.Name</c>. Pictures already stored in the old
    /// <c>site-files</c> stay in <c>attachments</c> and are still served - only new uploads of a picture
    /// are refused there, since the container now takes documents only.
    /// </para>
    /// <para>
    /// <c>Down</c> restores the old names (best effort, like any such rewrite: it cannot tell rows written
    /// under the new name after this ran from rows this migration touched). The SQL Server equivalent is in
    /// CHANGELOG.md.
    /// </para>
    /// </summary>
    public partial class Site_RenameFileContainers : Migration
    {
        private const string OldImages = "site-images";
        private const string NewImages = "images";
        private const string OldAttachments = "site-files";
        private const string NewAttachments = "attachments";
        private const string Path = "/api/site-public/files/";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RenameContainer(migrationBuilder, OldImages, NewImages);
            RenameContainer(migrationBuilder, OldAttachments, NewAttachments);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RenameContainer(migrationBuilder, NewImages, OldImages);
            RenameContainer(migrationBuilder, NewAttachments, OldAttachments);
        }

        private static void RenameContainer(MigrationBuilder migrationBuilder, string from, string to)
        {
            migrationBuilder.Sql(
                $"UPDATE \"SiteFileDescriptors\" SET \"ContainerName\" = '{to}' WHERE \"ContainerName\" = '{from}';");
            migrationBuilder.Sql(
                $"UPDATE \"SiteDirectoryDescriptors\" SET \"ContainerName\" = '{to}' WHERE \"ContainerName\" = '{from}';");

            RewritePaths(migrationBuilder, Path + from + "/", Path + to + "/");
            RewritePaths(migrationBuilder, Escape(Path + from + "/"), Escape(Path + to + "/"));

            // The quotes are part of the literal: "images" must not match inside "site-images" or any other name.
            migrationBuilder.Sql(
                $"UPDATE \"SiteFields\" SET \"Configuration\" = REPLACE(\"Configuration\", '\"{from}\"', '\"{to}\"') " +
                $"WHERE \"Configuration\" LIKE '%\"{from}\"%';");
        }

        /// <summary>The JSON-escaped spelling of <paramref name="value"/>: every <c>/</c> as <c>\/</c>.</summary>
        private static string Escape(string value)
        {
            return value.Replace("/", @"\/");
        }

        private static void RewritePaths(MigrationBuilder migrationBuilder, string from, string to)
        {
            migrationBuilder.Sql(
                $"UPDATE \"SiteContents\" SET \"FlexFields\" = REPLACE(\"FlexFields\", '{from}', '{to}') " +
                $"WHERE \"FlexFields\" LIKE '%{from}%';");

            migrationBuilder.Sql(
                $"UPDATE \"AbpSettings\" SET \"Value\" = REPLACE(\"Value\", '{from}', '{to}') " +
                $"WHERE \"Name\" LIKE 'Site.Branding.%' AND \"Value\" LIKE '%{from}%';");
        }
    }
}
