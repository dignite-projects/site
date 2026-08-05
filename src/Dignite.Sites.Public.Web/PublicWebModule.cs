using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Dignite.Sites.Localization;
using Microsoft.AspNetCore.Routing;
using Dignite.Sites.Public.Menus;
using Dignite.Sites.Public.Seo;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;
using Volo.Abp.VirtualFileSystem;
using Dignite.Sites.Permissions;

namespace Dignite.Sites.Public;

[DependsOn(
    typeof(SitesApplicationContractsModule),
    typeof(AbpAspNetCoreMvcUiThemeSharedModule),
    typeof(AbpMapperlyModule)
    )]
public class PublicWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(typeof(SitesResource), typeof(PublicWebModule).Assembly);
        });

        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(PublicWebModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new PublicMenuContributor());
        });

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<PublicWebModule>();
        });

        context.Services.AddMapperlyObjectMapper<PublicWebModule>();

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
