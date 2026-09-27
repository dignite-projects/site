using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Dignite.Site.Localization;
using Microsoft.AspNetCore.Routing;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Seo;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.Localization;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.VirtualFileSystem;
using Dignite.Abp.FlexFields.Web;
using Dignite.Abp.FlexFields.CKEditor.Web;
using Dignite.Abp.FlexFields.FileExplorer.Web;

namespace Dignite.Site.Public;

[DependsOn(
    typeof(SitePublicApplicationContractsModule),
    typeof(AbpAspNetCoreMvcUiThemeSharedModule),
    typeof(AbpMapperlyModule),
    // Read-only field display (<flex-field-view>) for SiteRenderController's Views - each self-registers
    // its own compiled Razor assembly part, nothing else to wire here.
    typeof(FlexFieldsWebModule),
    typeof(FlexFieldsCKEditorWebModule),
    typeof(FlexFieldsFileExplorerWebModule)
    )]
public class SitePublicWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(typeof(SiteResource), typeof(SitePublicWebModule).Assembly);
        });

        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(SitePublicWebModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        // First classic MVC View() consumer in this project (SiteRenderController) - explicit rather than
        // relying on it being present transitively, since AddControllersWithViews()/AddMvc()/AddRazorPages()
        // are additive/idempotent no matter how many modules across the graph call them.
        context.Services.AddControllersWithViews();

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SitePublicWebModule>();
        });

        context.Services.AddMapperlyObjectMapper<SitePublicWebModule>();

        Configure<RazorViewEngineOptions>(options =>
        {
            var currentTenantLazy = context.Services.GetServiceLazy<ICurrentTenant>();
            options.ViewLocationExpanders.Add(new TenantViewLocationExpander(currentTenantLazy));
        });

        // A site's own template texts, read from JSON beside its templates (GitHub issue #73). Registered
        // here and nowhere API-side - see SiteTemplateResource's remarks.
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<SiteTemplateResource>()
                .Contributors.Add(new SiteTemplateLocalizationContributor());
        });

        // Replace, not TryAdd: AddViewLocalization() (called by ABP's MVC module, which this module runs
        // after) has already registered the framework's ViewLocalizer.
        context.Services.Replace(ServiceDescriptor.Transient<IViewLocalizer, SiteViewLocalizer>());

        Configure<RazorPagesOptions>(options =>
        {
            //Configure authorization.
        });

        // Registers the constraint FeedController's catch-all route names. Without this the template
        // would fail to build at startup rather than silently mismatching, but the failure would be a
        // long way from the route that caused it - so it is wired next to the module that owns it.
        Configure<RouteOptions>(options =>
        {
            options.ConstraintMap[FeedPathRouteConstraint.Name] = typeof(FeedPathRouteConstraint);
        });

        if (hostingEnvironment.IsDevelopment())
        {
            context.Services.AddRazorPages()
                .AddRazorRuntimeCompilation();
        }
    }
}
