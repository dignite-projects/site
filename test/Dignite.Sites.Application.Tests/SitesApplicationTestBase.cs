using Volo.Abp.Modularity;

namespace Dignite.Sites;

/* Inherit from this class for your application layer tests.
 */
public abstract class SitesApplicationTestBase<TStartupModule> : SitesTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
