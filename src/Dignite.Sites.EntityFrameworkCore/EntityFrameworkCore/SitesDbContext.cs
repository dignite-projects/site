using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Sites.EntityFrameworkCore;

[ConnectionStringName(SitesDbProperties.ConnectionStringName)]
public class SitesDbContext : AbpDbContext<SitesDbContext>, ISitesDbContext
{
    /* Add DbSet for each Aggregate Root here. Example:
     * public DbSet<Question> Questions { get; set; }
     */

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
