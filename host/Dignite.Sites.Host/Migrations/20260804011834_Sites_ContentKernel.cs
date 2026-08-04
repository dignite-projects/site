using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Sites.Host.Migrations
{
    /// <inheritdoc />
    public partial class Sites_ContentKernel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SitesFieldGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SitesFieldGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SitesPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Route = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ContentPathPattern = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Template = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsHomePage = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_SitesPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SitesFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FieldTypeName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Configuration = table.Column<string>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_SitesFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SitesFields_SitesFieldGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SitesFieldGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SitesContentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Fields = table.Column<string>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_SitesContentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SitesContentTypes_SitesPages_PageId",
                        column: x => x.PageId,
                        principalTable: "SitesPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SitesContents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CultureName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PublishTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<byte>(type: "INTEGER", nullable: false),
                    FlexFields = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_SitesContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SitesContents_SitesContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "SitesContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SitesContents_SitesPages_PageId",
                        column: x => x.PageId,
                        principalTable: "SitesPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SitesContentFlexFieldIndexes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FieldId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValueType = table.Column<int>(type: "INTEGER", nullable: false),
                    StringValue = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    NumberValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    DateTimeValue = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BooleanValue = table.Column<bool>(type: "INTEGER", nullable: true),
                    GuidValue = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SitesContentFlexFieldIndexes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SitesContentFlexFieldIndexes_SitesContents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "SitesContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SitesContentFlexFieldIndexes_ContentId",
                table: "SitesContentFlexFieldIndexes",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_SitesContentFlexFieldIndexes_FieldId_ValueType_DateTimeValue",
                table: "SitesContentFlexFieldIndexes",
                columns: new[] { "FieldId", "ValueType", "DateTimeValue" });

            migrationBuilder.CreateIndex(
                name: "IX_SitesContentFlexFieldIndexes_FieldId_ValueType_NumberValue",
                table: "SitesContentFlexFieldIndexes",
                columns: new[] { "FieldId", "ValueType", "NumberValue" });

            migrationBuilder.CreateIndex(
                name: "IX_SitesContentFlexFieldIndexes_FieldId_ValueType_StringValue",
                table: "SitesContentFlexFieldIndexes",
                columns: new[] { "FieldId", "ValueType", "StringValue" });

            migrationBuilder.CreateIndex(
                name: "IX_SitesContents_ContentTypeId",
                table: "SitesContents",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SitesContents_PageId",
                table: "SitesContents",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_SitesContents_TenantId_PageId_ContentTypeId_Slug",
                table: "SitesContents",
                columns: new[] { "TenantId", "PageId", "ContentTypeId", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_SitesContents_TenantId_PageId_CultureName_Slug",
                table: "SitesContents",
                columns: new[] { "TenantId", "PageId", "CultureName", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SitesContents_TenantId_PageId_CultureName_Status_PublishTime",
                table: "SitesContents",
                columns: new[] { "TenantId", "PageId", "CultureName", "Status", "PublishTime" });

            migrationBuilder.CreateIndex(
                name: "IX_SitesContentTypes_PageId",
                table: "SitesContentTypes",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_SitesContentTypes_TenantId_PageId_Name",
                table: "SitesContentTypes",
                columns: new[] { "TenantId", "PageId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SitesFieldGroups_TenantId_Name",
                table: "SitesFieldGroups",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SitesFields_GroupId",
                table: "SitesFields",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SitesFields_TenantId_Name",
                table: "SitesFields",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SitesPages_TenantId_Name",
                table: "SitesPages",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SitesPages_TenantId_Route",
                table: "SitesPages",
                columns: new[] { "TenantId", "Route" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SitesContentFlexFieldIndexes");

            migrationBuilder.DropTable(
                name: "SitesFields");

            migrationBuilder.DropTable(
                name: "SitesContents");

            migrationBuilder.DropTable(
                name: "SitesFieldGroups");

            migrationBuilder.DropTable(
                name: "SitesContentTypes");

            migrationBuilder.DropTable(
                name: "SitesPages");
        }
    }
}
