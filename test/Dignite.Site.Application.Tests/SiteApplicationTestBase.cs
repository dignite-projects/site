using Volo.Abp.Modularity;

namespace Dignite.Site;

/* Inherit from this class for your application layer tests.
 */
public abstract class SiteApplicationTestBase<TStartupModule> : SiteTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
