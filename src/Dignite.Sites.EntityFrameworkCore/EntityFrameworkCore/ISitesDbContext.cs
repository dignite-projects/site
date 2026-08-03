using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Sites.EntityFrameworkCore;

[ConnectionStringName(SitesDbProperties.ConnectionStringName)]
public interface ISitesDbContext : IEfCoreDbContext
{
    /* Add DbSet for each Aggregate Root here. Example:
     * DbSet<Question> Questions { get; }
     */
}
