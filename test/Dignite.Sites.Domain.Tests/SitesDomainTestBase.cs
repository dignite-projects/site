using Volo.Abp.Modularity;

namespace Dignite.Sites;

/* Inherit from this class for your domain layer tests.
 */
public abstract class SitesDomainTestBase<TStartupModule> : SitesTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
