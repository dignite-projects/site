using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.CKEditor;
using Dignite.Abp.FlexFields.FileExplorer;
using Dignite.FlexFields.Site;
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
    // The CKEditor field type (registration name "CKEditor") - self-registers as IFieldType via DI once
    // referenced, same mechanism as the six built-in types and Site's own SeoFieldType (GitHub issue #43).
    typeof(FlexFieldsCKEditorModule),
    // The FileExplorer field type (registration name "FileExplorer") - same self-registering mechanism.
    // Its FileContainerName has to name a container the host actually configured (GitHub issue #41
    // stood up SiteFileContainerNames.Default); unlike CKEditor this field type is unusable until that
    // exists, which is why #41 had to land first (GitHub issue #42).
    typeof(FlexFieldsFileExplorerModule),
    // The Content, Matrix and Table field types (registration names "Content"/"Matrix"/"Table") - same
    // self-registering mechanism, but living in this repo rather than abp-modules (GitHub issue #49).
    typeof(FlexFieldsSiteModule),
    // IBrandingProvider, for JSON-LD's Organization/WebSite name and logo (GitHub issue #20).
    typeof(AbpUiModule)
)]
public class SiteDomainModule : AbpModule
{

}
