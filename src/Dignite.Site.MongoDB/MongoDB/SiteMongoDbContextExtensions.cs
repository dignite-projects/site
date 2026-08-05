using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dignite.Site.MongoDB;

public static class SiteMongoDbContextExtensions
{
    public static void ConfigureSite(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));
    }
}
