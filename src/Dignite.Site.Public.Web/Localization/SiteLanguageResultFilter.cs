using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// Prepares every view MVC renders for the site's languages (GitHub issue #75), registered globally by
/// <see cref="SitePublicWebModule"/>:
/// <list type="bullet">
/// <item>loads them (<see cref="SiteLanguageContext.GetLanguagesAsync"/>), so a view's synchronous callers -
/// <c>Url.SiteContent</c> - can read them;</item>
/// <item>makes the site's default language the last one <see cref="SiteTemplateResource"/>'s texts fall back
/// to (<see cref="SiteTemplateDefaultCulture"/>) - on every page, a Site page or not: a host's menu read on an
/// error page in a language the site does not serve still shows the default language's texts, not
/// keys.</item>
/// </list>
/// <para>
/// A result filter rather than middleware, because MVC runs after <c>UseMultiTenancy()</c> by construction -
/// the setting is per tenant - and because a result that renders no view, e.g. an API response, then never
/// reads the setting at all. Around the result's execution, not before it, so the ambient value is set in the
/// async flow that renders the view.
/// </para>
/// </summary>
public class SiteLanguageResultFilter : IAsyncAlwaysRunResultFilter, ITransientDependency
{
    public virtual async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!MayRenderView(context.Result))
        {
            await next();
            return;
        }

        var languages = await context.HttpContext.RequestServices
            .GetRequiredService<SiteLanguageContext>()
            .GetLanguagesAsync();

        using (SiteTemplateDefaultCulture.Use(languages.DefaultCultureName))
        {
            await next();
        }
    }

    /// <summary>
    /// False for the results known to render no view - API responses, redirects, files, bare status codes,
    /// authentication challenges. Anything else is prepared, a result type this filter has never heard of
    /// included: a custom result wrapping a view (<c>SiteRenderController</c>'s own is one) must find the
    /// languages loaded, while preparing one that renders nothing costs no more than a cached setting read.
    /// </summary>
    protected virtual bool MayRenderView(IActionResult result)
    {
        return result is not (ObjectResult or JsonResult or ContentResult or StatusCodeResult or FileResult
            // LocalRedirectResult is the one redirect that is not an IKeepTempDataResult.
            or EmptyResult or IKeepTempDataResult or LocalRedirectResult
            or ChallengeResult or ForbidResult or SignInResult or SignOutResult);
    }
}
