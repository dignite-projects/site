using System.Collections.Generic;
using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Dignite.FileExplorer.EntityFrameworkCore;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore.ValueComparers;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.EntityFrameworkCore.ValueConverters;

namespace Dignite.Site.EntityFrameworkCore;

public static class SiteDbContextModelCreatingExtensions
{
    public static void ConfigureSite(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        // A value object living inside ContentType's JSON column, not a table of its own.
        builder.Ignore<ContentTypeField>();

        builder.Entity<Page>(b =>
        {
            b.ToTable(SiteDbProperties.DbTablePrefix + "Pages", SiteDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(p => p.Name).IsRequired().HasMaxLength(PageConsts.MaxNameLength);
            b.Property(p => p.DisplayName).IsRequired().HasMaxLength(PageConsts.MaxDisplayNameLength);
            b.Property(p => p.Route).IsRequired().HasMaxLength(PageConsts.MaxRouteLength);
            b.Property(p => p.Template).HasMaxLength(PageConsts.MaxTemplateLength);
            b.Property(p => p.ContentTemplate).HasMaxLength(PageConsts.MaxContentTemplateLength);

            b.HasMany(p => p.ContentTypes).WithOne().HasForeignKey(ct => ct.PageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Self-referencing and declaratively Restrict, though that clause is largely decorative:
            // Page is soft-deleted, so a delete is an UPDATE and this ON DELETE clause never actually
            // fires. The real guard against deleting a page out from under its children is
            // PageManager.DeleteAsync's explicit check.
            b.HasOne<Page>().WithMany().HasForeignKey(p => p.ParentId).OnDelete(DeleteBehavior.Restrict);

            // Both unique per tenant. The route is what a request resolves against and the name is the
            // handle MCP tools and templates address a page by, so a duplicate of either is ambiguous
            // rather than merely untidy - worth a database constraint, not just a manager check.
            b.HasIndex(p => new { p.TenantId, p.Route }).IsUnique();
            b.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
            b.HasIndex(p => new { p.TenantId, p.ParentId });
        });

        builder.Entity<ContentType>(b =>
        {
            b.ToTable(SiteDbProperties.DbTablePrefix + "ContentTypes", SiteDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(ct => ct.Name).IsRequired().HasMaxLength(ContentTypeConsts.MaxNameLength);
            b.Property(ct => ct.DisplayName).IsRequired().HasMaxLength(ContentTypeConsts.MaxDisplayNameLength);
            b.Property(ct => ct.Description).HasMaxLength(ContentTypeConsts.MaxDescriptionLength);

            // The field arrangement is read as a whole with its type and never queried across types, so
            // it is a JSON column rather than a table. Mapped through the backing field because the
            // property is exposed as IReadOnlyList - callers change it through SetFields(), which is
            // what enforces the no-duplicate-field rule.
            b.Property<List<ContentTypeField>>("_fields")
                .HasField("_fields")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasColumnName("Fields")
                .HasConversion(new AbpJsonValueConverter<List<ContentTypeField>>())
                // Required: a value-converted collection is otherwise compared and snapshotted by
                // reference, so a change to the arrangement can be silently dropped.
                .Metadata.SetValueComparer(new ContentTypeFieldListValueComparer());

            b.HasIndex(ct => new { ct.TenantId, ct.PageId, ct.Name }).IsUnique();
        });

        builder.Entity<Field>(b =>
        {
            b.ToTable(SiteDbProperties.DbTablePrefix + "Fields", SiteDbProperties.DbSchema);
            b.ConfigureByConvention();

            // The kernel maps Name / DisplayName / Description / FieldTypeName / Configuration, with the
            // lengths FlexFieldConsts defines. Restating them here would create a second set that could
            // drift from what is actually applied.
            b.ConfigureFlexField();

            b.Property(f => f.GroupName).HasMaxLength(FieldConsts.MaxGroupNameLength);

            // Unique because a field's name doubles as the key its values are stored under in every
            // content's bag - two definitions sharing a name would be two definitions of one stored value.
            b.HasIndex(f => new { f.TenantId, f.Name }).IsUnique();

            // Non-unique: grouping is a filing label, so many fields share one. Indexed because the
            // library listing filters and orders by it.
            b.HasIndex(f => new { f.TenantId, f.GroupName });
        });

        builder.Entity<Content>(b =>
        {
            b.ToTable(SiteDbProperties.DbTablePrefix + "Contents", SiteDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(c => c.CultureName).IsRequired().HasMaxLength(ContentConsts.MaxCultureNameLength);
            b.Property(c => c.Slug).IsRequired().HasMaxLength(ContentConsts.MaxSlugLength);

            // The kernel maps the authoritative value bag to a single JSON column, with the value
            // comparer that makes in-place SetField/RemoveField on a loaded entity actually persist.
            b.ConfigureFlexFieldsProperty();

            b.HasOne<Page>().WithMany().HasForeignKey(c => c.PageId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<ContentType>().WithMany().HasForeignKey(c => c.ContentTypeId).OnDelete(DeleteBehavior.Restrict);

            // 总体设计 §2.5's uniqueness rule, and the lookup route resolution performs on every request.
            b.HasIndex(c => new { c.TenantId, c.PageId, c.CultureName, c.Slug }).IsUnique();

            // The translation-group key (§2.4). Contents sharing it in different languages are the same
            // content, which is what hreflang and the language switcher aggregate on - so this grouping
            // has to be indexed even though nothing declares it as a constraint.
            b.HasIndex(c => new { c.TenantId, c.PageId, c.ContentTypeId, c.Slug });

            // The list query: everything published under a page, in one language, newest first.
            b.HasIndex(c => new { c.TenantId, c.PageId, c.CultureName, c.Status, c.PublishTime });
        });

        builder.Entity<ContentFlexFieldIndex>(b =>
        {
            b.ToTable(SiteDbProperties.DbTablePrefix + "ContentFlexFieldIndexes", SiteDbProperties.DbSchema);
            b.ConfigureByConvention();

            // The kernel maps the typed value slots; the table, the foreign key and the indexes below
            // are Site' own - the kernel deliberately declares no relationship.
            b.ConfigureFlexFieldIndex();

            // Cascade: an index row is derived state belonging to one content and is meaningless once
            // that content is gone.
            b.HasOne<Content>().WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Cascade);

            // Replacing one content's rows on every save - the manager's read-then-delete path.
            b.HasIndex(x => x.ContentId);

            // The query path: filter by field, then by the typed slot the condition names. FieldId leads
            // because every condition constrains it, so it is what makes the scan selective.
            b.HasIndex(x => new { x.FieldId, x.ValueType, x.StringValue });
            b.HasIndex(x => new { x.FieldId, x.ValueType, x.NumberValue });
            b.HasIndex(x => new { x.FieldId, x.ValueType, x.DateTimeValue });
        });

        // Dignite.FileExplorer (GitHub issue #41's follow-up) is consumed the same way FlexFields is
        // (§8.2) - part of Site's own EF Core wiring, not something Host bolts on separately. Whatever
        // DbContext calls ConfigureSite() - SiteDbContext for standalone use, SiteHostDbContext once a host
        // replaces it - gets FileExplorer's tables for free from this one call.
        builder.ConfigureFileExplorer();
    }
}
