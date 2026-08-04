using Dignite.Sites.ContentTypes;
using Dignite.Sites.Contents;
using Dignite.Sites.Fields;
using Dignite.Sites.Pages;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Sites.EntityFrameworkCore;

[ConnectionStringName(SitesDbProperties.ConnectionStringName)]
public class SitesDbContext : AbpDbContext<SitesDbContext>, ISitesDbContext
{
    public DbSet<Page> Pages { get; set; } = default!;

    public DbSet<ContentType> ContentTypes { get; set; } = default!;

    public DbSet<Field> Fields { get; set; } = default!;

    public DbSet<FieldGroup> FieldGroups { get; set; } = default!;

    public DbSet<Content> Contents { get; set; } = default!;

    public DbSet<ContentFlexFieldIndex> ContentFlexFieldIndexes { get; set; } = default!;

    public SitesDbContext(DbContextOptions<SitesDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureSites();
    }
}
