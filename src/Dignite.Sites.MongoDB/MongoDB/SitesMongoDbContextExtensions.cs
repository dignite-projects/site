using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dignite.Sites.MongoDB;

public static class SitesMongoDbContextExtensions
{
    public static void ConfigureSites(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));
    }
}
