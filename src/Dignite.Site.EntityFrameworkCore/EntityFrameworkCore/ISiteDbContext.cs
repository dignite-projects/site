using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.EntityFrameworkCore;

[ConnectionStringName(SiteDbProperties.ConnectionStringName)]
public interface ISiteDbContext : IEfCoreDbContext
{
    DbSet<Page> Pages { get; }

    DbSet<ContentType> ContentTypes { get; }

    DbSet<Field> Fields { get; }

    DbSet<FieldGroup> FieldGroups { get; }

    DbSet<Content> Contents { get; }

    /// <summary>
    /// The derived query index. Exposed on the interface because FlexFields' EF Core base classes reach
    /// it through <c>IDbContextProvider&lt;ISiteDbContext&gt;</c> - both the index manager that writes
    /// it and the query executor that reads it are generic over this interface, which is how the kernel
    /// avoids ever naming a concrete DbContext.
    /// </summary>
    DbSet<ContentFlexFieldIndex> ContentFlexFieldIndexes { get; }
}
