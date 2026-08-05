using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <inheritdoc />
    public partial class Site_ContentKernel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteFieldGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteFieldGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SitePages",
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
                    table.PrimaryKey("PK_SitePages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteFields",
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
                    table.PrimaryKey("PK_SiteFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteFields_SiteFieldGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SiteFieldGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SiteContentTypes",
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
                    table.PrimaryKey("PK_SiteContentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteContentTypes_SitePages_PageId",
                        column: x => x.PageId,
                        principalTable: "SitePages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteContents",
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
                    table.PrimaryKey("PK_SiteContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteContents_SiteContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "SiteContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteContents_SitePages_PageId",
                        column: x => x.PageId,
                        principalTable: "SitePages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteContentFlexFieldIndexes",
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
                    table.PrimaryKey("PK_SiteContentFlexFieldIndexes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteContentFlexFieldIndexes_SiteContents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "SiteContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteContentFlexFieldIndexes_ContentId",
                table: "SiteContentFlexFieldIndexes",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteContentFlexFieldIndexes_FieldId_ValueType_DateTimeValue",
                table: "SiteContentFlexFieldIndexes",
                columns: new[] { "FieldId", "ValueType", "DateTimeValue" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteContentFlexFieldIndexes_FieldId_ValueType_NumberValue",
                table: "SiteContentFlexFieldIndexes",
                columns: new[] { "FieldId", "ValueType", "NumberValue" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteContentFlexFieldIndexes_FieldId_ValueType_StringValue",
                table: "SiteContentFlexFieldIndexes",
                columns: new[] { "FieldId", "ValueType", "StringValue" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteContents_ContentTypeId",
                table: "SiteContents",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteContents_PageId",
                table: "SiteContents",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteContents_TenantId_PageId_ContentTypeId_Slug",
                table: "SiteContents",
                columns: new[] { "TenantId", "PageId", "ContentTypeId", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteContents_TenantId_PageId_CultureName_Slug",
                table: "SiteContents",
                columns: new[] { "TenantId", "PageId", "CultureName", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteContents_TenantId_PageId_CultureName_Status_PublishTime",
                table: "SiteContents",
                columns: new[] { "TenantId", "PageId", "CultureName", "Status", "PublishTime" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteContentTypes_PageId",
                table: "SiteContentTypes",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteContentTypes_TenantId_PageId_Name",
                table: "SiteContentTypes",
                columns: new[] { "TenantId", "PageId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteFieldGroups_TenantId_Name",
                table: "SiteFieldGroups",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteFields_GroupId",
                table: "SiteFields",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteFields_TenantId_Name",
                table: "SiteFields",
                columns: new[] { "TenantId", "Name" },
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteContentFlexFieldIndexes");

            migrationBuilder.DropTable(
                name: "SiteFields");

            migrationBuilder.DropTable(
                name: "SiteContents");

            migrationBuilder.DropTable(
                name: "SiteFieldGroups");

            migrationBuilder.DropTable(
                name: "SiteContentTypes");

            migrationBuilder.DropTable(
                name: "SitePages");
        }
    }
}
