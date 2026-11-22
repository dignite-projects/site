using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dignite.Site.MongoDB;

public static class SiteMongoDbContextExtensions
{
    /// <summary>
    /// Site keeps no entity of its own in MongoDB: its files are Blob Management's. The extension stays as the place
    /// a MongoDB-kept entity of Site's would be configured.
    /// </summary>
    public static void ConfigureSite(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));
    }
}
