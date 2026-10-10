using Dignite.Abp.FileStoring;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.CKEditor;
using Dignite.FlexFields.Site;
using Volo.Abp.Domain;
using Volo.Abp.Features;
using Volo.Abp.Modularity;
using Volo.Abp.UI;

namespace Dignite.Site;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(AbpFeaturesModule),
    typeof(SiteDomainSharedModule),
    // The entire field mechanism - field types, the value bag, validation, the derived query index, and
    // rename migration - comes from here (总体设计 §8.2). Site contributes only Field, Content's bag and
    // one provider; the kernel itself owns no field or host model.
    typeof(FlexFieldsDomainModule),
    // The CKEditor field type (registration name "CKEditor") - self-registers as IFieldType via DI once
    // referenced, same mechanism as the six built-in types and Site's own SeoFieldType (GitHub issue #43).
    typeof(FlexFieldsCKEditorModule),
    // The Content, Seo and file field types (FileFieldType keeps the registration key it had in
    // abp-modules) - same self-registering mechanism, but living in this repo (GitHub issue #49). A file
    // field's FileContainerName names one of SiteFileContainerNames (GitHub issue #42).
    // Matrix and Table used to live here too, until flex-fields shipped them as kernel built-ins at
    // 10.0.0-rc.16.
    typeof(FlexFieldsSiteModule),
    // IFileStorer - the upload pipeline the file library (Files/, Directories/) stores bytes through.
    typeof(DigniteAbpFileStoringModule),
    // IBrandingProvider, for JSON-LD's Organization/WebSite name and logo (GitHub issue #20).
    typeof(AbpUiModule)
)]
public class SiteDomainModule : AbpModule
{

}
