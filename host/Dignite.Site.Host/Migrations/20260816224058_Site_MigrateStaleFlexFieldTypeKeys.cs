using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Site.Host.Migrations
{
    /// <summary>
    /// Data-only fix-up for FlexFields' 2026-08-17 registration-key rename (abp-modules/flex-fields
    /// CLAUDE.md, "Registration keys and configuration keys are persisted data"): rewrites any
    /// <c>SiteFields.FieldTypeName</c>/<c>Configuration</c> key prefix still under a pre-rename name.
    /// <c>Select</c> was already consistent and needs no entry. <c>Down</c> is a best-effort mirror -
    /// like any such rewrite, it cannot distinguish rows written under the new keys after this ran from
    /// rows this migration itself touched.
    /// </summary>
    public partial class Site_MigrateStaleFlexFieldTypeKeys : Migration
    {
        private static readonly (string OldTypeName, string NewTypeName, string OldConfigPrefix, string NewConfigPrefix)[] Renames =
        {
            ("TextEdit", "Text", "TextEdit.", "Text."),
            ("NumericEdit", "Number", "NumericEditField.", "Number."),
            ("DateEdit", "DateTime", "DateEdit.", "DateTime."),
            ("Switch", "Boolean", "Switch.", "Boolean."),
            ("TreeView", "Tree", "TreeView.", "Tree."),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var rename in Renames)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"SiteFields\" SET \"FieldTypeName\" = '{rename.NewTypeName}' WHERE \"FieldTypeName\" = '{rename.OldTypeName}';");

                migrationBuilder.Sql(
                    $"UPDATE \"SiteFields\" SET \"Configuration\" = REPLACE(\"Configuration\", '\"{rename.OldConfigPrefix}', '\"{rename.NewConfigPrefix}') " +
                    $"WHERE \"Configuration\" LIKE '%\"{rename.OldConfigPrefix}%';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var rename in Renames)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"SiteFields\" SET \"Configuration\" = REPLACE(\"Configuration\", '\"{rename.NewConfigPrefix}', '\"{rename.OldConfigPrefix}') " +
                    $"WHERE \"Configuration\" LIKE '%\"{rename.NewConfigPrefix}%';");

                migrationBuilder.Sql(
                    $"UPDATE \"SiteFields\" SET \"FieldTypeName\" = '{rename.OldTypeName}' WHERE \"FieldTypeName\" = '{rename.NewTypeName}';");
            }
        }
    }
}
