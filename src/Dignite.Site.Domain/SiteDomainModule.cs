using Dignite.Abp.FlexFields;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;
using Volo.Abp.UI;

namespace Dignite.Site;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(SiteDomainSharedModule),
    // The entire field mechanism - field types, the value bag, validation, the derived query index, and
    // rename migration - comes from here (总体设计 §8.2). Site contributes only Field, Content's bag and
    // one provider; the kernel itself owns no field or host model.
    typeof(FlexFieldsDomainModule),
    // IBrandingProvider, for JSON-LD's Organization/WebSite name and logo (GitHub issue #20).
    typeof(AbpUiModule)
)]
public class SiteDomainModule : AbpModule
{

}
