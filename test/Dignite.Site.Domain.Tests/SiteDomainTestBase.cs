using Volo.Abp.Modularity;

namespace Dignite.Site;

/* Inherit from this class for your domain layer tests.
 */
public abstract class SiteDomainTestBase<TStartupModule> : SiteTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
