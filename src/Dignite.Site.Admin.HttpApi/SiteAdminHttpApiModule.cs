using Localization.Resources.AbpUi;
using Dignite.Site.Admin.Files;
using Dignite.Site.Localization;
using Dignite.Site.Common;
using System.Net;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Site.Admin;

[DependsOn(
    typeof(SiteAdminApplicationContractsModule),
    typeof(SiteCommonHttpApiModule),
    typeof(AbpAspNetCoreMvcModule))]
public class SiteAdminHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(SiteAdminHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The upload links are signed with it (FileUploadTicketProtector). Added by every application that has
        // authentication already, and adding it again changes nothing: it keeps the keys the host configured.
        context.Services.AddDataProtection();

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<SiteResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });

        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            // An upload is multipart/form-data with the rest in the query string; without this ABP's
            // service convention would bind CreateFileInput from a JSON body instead.
            options.ConventionalControllers.FormBodyBindingIgnoredTypes.Add(typeof(CreateFileInput));
        });

        Configure<AbpExceptionHttpStatusCodeOptions>(options =>
        {
            // A preview that cannot be rendered is not a request the user may not make, which ABP's 403 for a business
            // exception would say: the path answers nothing (404, as for an entity that is not there), the theme fails
            // on it (422), or this application has nothing to render with (501, as for what is not implemented). The
            // other theme codes keep ABP's 403 - the theme editor reads a 409 as another save of the same file.
            options.Map(SiteErrorCodes.SiteTemplatePreviewPathNotFound, HttpStatusCode.NotFound);
            options.Map(SiteErrorCodes.SiteTemplateDidNotRenderSiteHead, HttpStatusCode.UnprocessableEntity);
            options.Map(SiteErrorCodes.SiteTemplatePreviewRenderFailed, HttpStatusCode.UnprocessableEntity);
            options.Map(SiteErrorCodes.SiteThemePreviewPageNotScriban, HttpStatusCode.UnprocessableEntity);
            options.Map(SiteErrorCodes.SiteTemplatePreviewUnavailable, HttpStatusCode.NotImplemented);
        });
    }
}
