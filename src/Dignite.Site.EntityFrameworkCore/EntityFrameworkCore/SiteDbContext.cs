using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.EntityFrameworkCore;

[ConnectionStringName(SiteDbProperties.ConnectionStringName)]
public class SiteDbContext : AbpDbContext<SiteDbContext>, ISiteDbContext
{
    public DbSet<Page> Pages { get; set; } = default!;

    public DbSet<ContentType> ContentTypes { get; set; } = default!;

    public DbSet<Field> Fields { get; set; } = default!;

    public DbSet<FieldGroup> FieldGroups { get; set; } = default!;

    public DbSet<Content> Contents { get; set; } = default!;

    public DbSet<ContentFlexFieldIndex> ContentFlexFieldIndexes { get; set; } = default!;

    public SiteDbContext(DbContextOptions<SiteDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureSite();
    }
}
