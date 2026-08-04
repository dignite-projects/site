using Dignite.Abp.FlexFields;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dignite.Sites;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(SitesDomainSharedModule),
    // The entire field mechanism - field types, the value bag, validation, the derived query index, and
    // rename migration - comes from here (总体设计 §8.2). Sites contributes only Field, Content's bag and
    // one provider; the kernel itself owns no field or host model.
    typeof(FlexFieldsDomainModule)
)]
public class SitesDomainModule : AbpModule
{

}
