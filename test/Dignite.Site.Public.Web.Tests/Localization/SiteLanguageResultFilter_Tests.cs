using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// Which results <see cref="SiteLanguageResultFilter"/> prepares (GitHub issue #75). The template-text side of
/// it is covered in <c>SiteTemplateLocalization_Tests</c>.
/// </summary>
public class SiteLanguageResultFilter_Tests
{
    /// <returns>The default culture the result saw while it executed.</returns>
    private static async Task<string?> ExecuteAsync(SiteLanguageTestHost host, IActionResult result)
    {
        var actionContext = new ActionContext(host.HttpContext, new RouteData(), new ActionDescriptor());
        var filters = new List<IFilterMetadata>();
        var context = new ResultExecutingContext(actionContext, filters, result, controller: new object());

        string? defaultCulture = "not executed";
        await new SiteLanguageResultFilter().OnResultExecutionAsync(context, () =>
        {
            defaultCulture = SiteTemplateDefaultCulture.Current;
            return Task.FromResult(new ResultExecutedContext(actionContext, filters, result, new object()));
        });

        return defaultCulture;
    }

    /// <summary>
    /// A result type the filter has never heard of may wrap a view - SiteRenderController's own does - so it is
    /// prepared: its view's synchronous callers find the languages loaded.
    /// </summary>
    [Fact]
    public async Task Should_Prepare_A_Result_It_Does_Not_Know()
    {
        var host = new SiteLanguageTestHost("zh-Hans,ja", "/ja/about");

        (await ExecuteAsync(host, new UnknownResult())).ShouldBe("zh-Hans");
        host.LanguageContext.Languages.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Leave_Results_Known_To_Render_No_View_Alone()
    {
        foreach (IActionResult result in new IActionResult[]
                 {
                     new ObjectResult(null), new JsonResult(null), new ContentResult(), new NotFoundResult(),
                     new EmptyResult(), new RedirectResult("/ja"), new LocalRedirectResult("/ja"),
                     new RedirectToActionResult("Index", "Home", null), new RedirectToPageResult("/Index"),
                     new ChallengeResult(), new ForbidResult(), new FileContentResult(new byte[0], "text/plain")
                 })
        {
            var host = new SiteLanguageTestHost("zh-Hans,ja", "/ja/about");

            (await ExecuteAsync(host, result)).ShouldBeNull(result.GetType().Name);
            host.Settings.ReadCount.ShouldBe(0, result.GetType().Name);
        }
    }

    private class UnknownResult : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context) => Task.CompletedTask;
    }
}
